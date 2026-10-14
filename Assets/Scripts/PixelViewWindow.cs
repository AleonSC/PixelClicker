// ---- System notes ----
// PixelViewWindow.cs
//   FREE CAM (4th tick box, PixelFreeCam.cs): a detached camera flown by hand (WASD, Space / C, Shift, middle drag to look); never saved as the player's view, no zoom slider, no flat view.
//   The View window (component added by PixelClicker.Awake; opens ONLY with its key, PixelAction.ViewWindow, default V; closes with the
//   key, its X or Escape (PixelWindows 130); a small draggable window that does not pause the game; disableViewWindow). It replaces the
//   old F (view) and X (zoom) keys, which are retired: View tick boxes - First person, Orbital, Top down (the normal view) - where ticking
//   one unticks the others (PixelViewWindow.SetView: leaves the current view, enters the new one; an orbital <-> top down switch just
//   re-aims the same camera, PixelOrbitCamera.SetTopDown); an 'Orthographic (flat) view' tick box (PixelOrthoView.Enabled, the same
//   setting as Settings > Flat view; greyed out in first person, which is always 3D); a Zoom slider (the normal view's PixelZoom.SetTarget,
//   or the orbit / top down camera's DistanceFraction; not in first person); and, in Orbital / Top down, the orbit controls: Auto orbit tick
//   box + speed slider, and hold buttons for turning (and, in Orbital only, tilting). Each view row shows its CONTROLS as a vertical hover tooltip (AddViewTip; the old hint lines at the bottom of the screen for first person and orbit are gone). While the window is open first person gives the
//   mouse back (PixelFirstPerson checks PixelViewWindow.IsOpen). The choices are saved with the save file's settings (PlayerPrefs
//   PixelClicker.Setting.ViewKind / ViewBaseZoom / ViewAutoOrbit / ViewAutoSpeed / ViewScrollInvert, listed in PixelSettingsSync): ApplySaved puts the
//   view back once the title screen and camera intro are over. There is no separate Default view any more: TOP DOWN is the normal view (Default only means 'no view in use for a moment': the intro, a takeover minigame, first person ending) - Update puts the player's view back (ApplySaved every 0.4 s while Default; nothing saved = startView Top down at startZoom 1). Zoom 1.0 is the BASE view (PixelOrbitCamera: the scene camera is put 6 units from the cube at 89 degrees at start, useBaseView / baseDistance / baseTilt / baseTurn); the slider and the wheel zoom between 0.35x and 2x of that and the zoom is saved (ViewBaseZoom; the wheel too). 'Invert zoom scroll' tick (orbit section, PlayerPrefs ViewScrollInvert, default ON) flips the wheel direction in PixelOrbitCamera (scroll up = zoom in). (first person is never restored by itself; orthographic is the existing FlatView setting).
// ---------------------------------------------------
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum PixelViewKind { Default, FirstPerson, Orbital, TopDown, FreeCam }

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

    [Header("Start view (used until the player picks one: no saved view setting)")]
    [Tooltip("The view a fresh save starts in (and the one the camera returns to after a takeover minigame). Top down is the normal view of the game.")]
    [SerializeField] private PixelViewKind startView = PixelViewKind.TopDown;

    [Min(0.1f)]
    [Tooltip("Zoom of that view, as a fraction of the base distance (1 = the base view from PixelOrbitCamera 'Base Distance'; smaller = closer, up to 2 = twice as far).")]
    [SerializeField] private float startZoom = 1f;

    // Saved with the save file's settings (PixelSettingsSync lists these keys): the view, the zoom of the normal view, the orbit / top down zoom and auto orbit.
    private const string PrefKind = "PixelClicker.Setting.ViewKind", PrefOrbitZoom = "PixelClicker.Setting.ViewBaseZoom",   // (the old ViewOrbitZoom was relative to the scene camera and is no longer read)
                         PrefAuto = "PixelClicker.Setting.ViewAutoOrbit", PrefSpeed = "PixelClicker.Setting.ViewAutoSpeed",
                         PrefInvert = "PixelClicker.Setting.ViewScrollInvert";

    /// <summary>The mouse wheel's zoom direction in the orbital / top down views is flipped (scroll up = zoom in). On by default; a tick box in the View window turns it off.</summary>
    public static bool InvertScroll { get; private set; } = true;

    private static PixelViewWindow instance;
    private bool pendingApply;
    private float nextAuto, lastSavedZoom = -1f;

    /// <summary>A save file with its own settings was loaded (or the game just started): the saved view is applied again as soon as it can be.</summary>
    public static void ReloadFromPrefs()
    {
        InvertScroll = PlayerPrefs.GetInt(PrefInvert, 1) != 0;
        if (instance != null) instance.pendingApply = true;
    }

    private static void SaveKind(PixelViewKind kind) { if (kind == PixelViewKind.FreeCam) return; PlayerPrefs.SetInt(PrefKind, kind == PixelViewKind.FirstPerson ? 0 : (int)kind); }   // (the free cam is never the saved view)   // (first person is never restored by itself)
    private static Vector2 savedPosition = new Vector2(-40f, 0f);

    /// <summary>True while the window is open (first person gives the mouse back meanwhile).</summary>
    public static bool IsOpen => instance != null && PixelPop.IsOpen(instance.window);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; savedPosition = new Vector2(-40f, 0f); InvertScroll = true; }

    /// <summary>The view in use right now.</summary>
    public static PixelViewKind CurrentView =>
        PixelFirstPerson.Active ? PixelViewKind.FirstPerson :
        PixelFreeCam.Active ? PixelViewKind.FreeCam :
        PixelOrbitCamera.Active ? (PixelOrbitCamera.TopDown ? PixelViewKind.TopDown : PixelViewKind.Orbital) : PixelViewKind.Default;

    /// <summary>Switches to a view: leaves the current one and enters the new one. Returns false if the new view can't start now (the game is back to the Default view then).</summary>
    public static bool SetView(PixelViewKind kind, bool announceFailure = true)
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
        else if (cur == PixelViewKind.FreeCam) PixelFreeCam.Leave();
        else if (cur != PixelViewKind.Default && orbit != null) orbit.Cancel();

        bool ok = true;
        switch (kind)
        {
            case PixelViewKind.FirstPerson: ok = PixelFirstPerson.TryEnter(); break;
            case PixelViewKind.Orbital: ok = orbit != null && orbit.Enter(false); break;
            case PixelViewKind.TopDown: ok = orbit != null && orbit.Enter(true); break;
            case PixelViewKind.FreeCam: ok = PixelFreeCam.TryEnter(); break;
        }
        if (!ok) { if (announceFailure) PixelHints.Announce("That view can't start right now"); return false; }
        return true;
    }

    private PixelClicker clicker;
    private GameObject canvasRoot, window;
    private RectTransform windowRect;
    private TMP_FontAsset font;
    private readonly Toggle[] viewTicks = new Toggle[4];   // First person, Orbital, Top down, Free cam
    private Toggle orthoTick, autoTick, invertTick;
    private RectTransform tipRect;
    private TMP_Text tipLabel;
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
        InvertScroll = PlayerPrefs.GetInt(PrefInvert, 1) != 0;
        pendingApply = true;   // the saved view, or the default view when nothing is saved
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
        if (!blocked && !PixelCameraIntro.Moving && !PixelMinigame.TakeoverActive)
        {
            if (pendingApply) ApplySaved();
            else if (CurrentView == PixelViewKind.Default && Time.unscaledTime >= nextAuto)
            {
                nextAuto = Time.unscaledTime + 0.4f;
                ApplySaved();   // no view is in use (the intro, a takeover minigame or first person just ended): back to the player's view (Top down by default)
            }
        }
        PixelOrbitCamera orbitNow = PixelOrbitCamera.Instance;
        if (PixelOrbitCamera.Active && orbitNow != null)   // the wheel zooms too: remember where it ended up
        {
            float f = orbitNow.DistanceFraction;
            if (Mathf.Abs(f - lastSavedZoom) > 0.004f) { lastSavedZoom = f; PlayerPrefs.SetFloat(PrefOrbitZoom, f); }
        }
        if (IsOpen && blocked) { Close(); return; }
        if (!blocked && !PixelKeys.Typing && !PixelTutorialDim.Active && PixelKeys.Pressed(PixelAction.ViewWindow))
        {
            if (IsOpen) Close(); else Open();
        }
        if (!IsOpen) return;
        Sync();
    }

    /// <summary>Puts the saved view back: the view itself, then its zoom and the orbit controls.</summary>
    private void ApplySaved()
    {
        pendingApply = false;
        PixelViewKind kind = (PixelViewKind)Mathf.Clamp(PlayerPrefs.GetInt(PrefKind, (int)startView), 0, 3);
        if (kind == PixelViewKind.FirstPerson || kind == PixelViewKind.Default) kind = PixelViewKind.TopDown;   // (first person is never restored by itself; the old Default view is Top down now)
        if (CurrentView != kind && !SetView(kind, false)) return;   // (the camera could not start it just now: Update tries again in a moment)
        if (CurrentView == PixelViewKind.Orbital || CurrentView == PixelViewKind.TopDown)
        {
            PixelOrbitCamera orbit = PixelOrbitCamera.Instance;
            if (orbit != null)
            {
                orbit.DistanceFraction = PlayerPrefs.GetFloat(PrefOrbitZoom, startZoom);
                lastSavedZoom = orbit.DistanceFraction;
                if (PlayerPrefs.HasKey(PrefSpeed)) orbit.AutoSpeed = PlayerPrefs.GetFloat(PrefSpeed, 10f);
                orbit.AutoOrbit = PlayerPrefs.GetInt(PrefAuto, 0) == 1;
            }
        }
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
        // (Top down is the normal view: it also shows as ticked for the moment no view is in use, e.g. during the intro)
        if (viewTicks[0] != null) viewTicks[0].SetIsOnWithoutNotify(cur == PixelViewKind.FirstPerson);
        if (viewTicks[1] != null) viewTicks[1].SetIsOnWithoutNotify(cur == PixelViewKind.Orbital);
        if (viewTicks[2] != null) viewTicks[2].SetIsOnWithoutNotify(cur == PixelViewKind.TopDown || cur == PixelViewKind.Default);
        if (viewTicks[3] != null) viewTicks[3].SetIsOnWithoutNotify(cur == PixelViewKind.FreeCam);

        bool free = cur == PixelViewKind.FreeCam;
        bool fp = cur == PixelViewKind.FirstPerson || free;   // (first person and the free cam are always the 3D view, and have no zoom slider)
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
                invertTick.SetIsOnWithoutNotify(InvertScroll);
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

    /// <summary>Shows 'text' (one control per line) in the tooltip box beside the window while the mouse is over 'row' (the box sits left of the window, or right of it when there is no room).</summary>
    private void AddViewTip(GameObject row, float rowY, string text)
    {
        PixelHoverTip hover = row.GetComponent<PixelHoverTip>();
        if (hover == null) hover = row.AddComponent<PixelHoverTip>();
        hover.onEnter = () =>
        {
            if (tipRect == null) return;
            tipLabel.text = PixelKeys.Replace(text);
            Vector2 pref = tipLabel.GetPreferredValues(tipLabel.text, 520f, 0f);
            tipRect.sizeDelta = new Vector2(Mathf.Min(520f, pref.x) + 32f, pref.y + 24f);
            Vector3[] corners = new Vector3[4];
            windowRect.GetWorldCorners(corners);
            bool left = corners[0].x / Mathf.Max(1, Screen.width) * 1920f > tipRect.sizeDelta.x + 24f;
            tipRect.anchorMin = tipRect.anchorMax = new Vector2(left ? 0f : 1f, 1f);
            tipRect.pivot = new Vector2(left ? 1f : 0f, 1f);
            tipRect.anchoredPosition = new Vector2(left ? -12f : 12f, -rowY);
            tipRect.gameObject.SetActive(true);
            tipRect.SetAsLastSibling();
        };
        hover.onExit = () => { if (tipRect != null) tipRect.gameObject.SetActive(false); };
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

        // The controls of each view show as a vertical tooltip beside the window while the mouse is over its row (nothing is drawn at the bottom of the screen any more).
        GameObject tip = new GameObject("View Tip", typeof(RectTransform), typeof(Image));
        tip.transform.SetParent(window.transform, false);
        tipRect = tip.GetComponent<RectTransform>();
        tip.GetComponent<Image>().raycastTarget = false;
        PixelUIKit.StyleWindow(tip.GetComponent<Image>(), new Color(0.06f, 0.08f, 0.13f, 0.97f));
        tipLabel = Label(tip.transform, "", 22f, FontStyles.Normal, Color.white, TextAlignmentOptions.TopLeft);
        tipLabel.raycastTarget = false;
        tipLabel.enableAutoSizing = false;   // (the box is sized to the text instead)
        tipLabel.fontSize = 22f;
        tipLabel.overflowMode = TextOverflowModes.Overflow;
        PixelUIKit.Stretch(tipLabel.rectTransform);
        tipLabel.rectTransform.offsetMin = new Vector2(16f, 12f);
        tipLabel.rectTransform.offsetMax = new Vector2(-16f, -12f);
        tip.SetActive(false);
        string[] tips =
        {
            "<b>First person controls</b>\nWASD  -  move\nShift  -  run\nSpace  -  jump\nClick the cube  -  click it\nRight-click a cube  -  carry it\n(again = drop, left-click = throw)\nEsc  -  leave\n{key:ViewWindow}  -  this window",
            "<b>Orbital controls</b>\nMiddle mouse drag  -  turn and tilt\nArrow keys  -  turn and tilt\nCtrl + mouse wheel  -  zoom\nTurn / tilt buttons and Auto orbit  -  below\n{key:ViewWindow}  -  this window",
            "<b>Top down controls</b>\nMiddle mouse drag  -  turn\nLeft / right arrow keys  -  turn\nCtrl + mouse wheel  -  zoom\nTurn buttons and Auto orbit  -  below\n{key:ViewWindow}  -  this window",
            "<b>Free cam controls</b>\nW / A / S / D  -  fly\nSpace / C  -  up / down\nShift  -  fast\nMiddle mouse drag or arrow keys  -  look\nCtrl + mouse wheel  -  flying speed\n{key:ViewWindow}  -  this window",
        };

        // --- Views: ticking one unticks the others.
        string[] names = { "First person", "Orbital", "Top down", "Free cam" };
        PixelViewKind[] kinds = { PixelViewKind.FirstPerson, PixelViewKind.Orbital, PixelViewKind.TopDown, PixelViewKind.FreeCam };
        for (int i = 0; i < names.Length; i++)
        {
            PixelViewKind kind = kinds[i];
            viewTicks[i] = TickRow(window.transform, y, names[i], on =>
            {
                if (on && SetView(kind)) SaveKind(kind);
                Sync();   // (a tick box can't be switched off by clicking it: one view is always in use)
            }, out GameObject viewRow);
            AddViewTip(viewRow, y, tips[i]);
            y += RowH + 6f;
        }
        y += 8f;

        // --- Flat (orthographic) view: not in first person.
        orthoTick = TickRow(window.transform, y, "Orthographic (flat) view", on => { PixelOrthoView.Enabled = on; Sync(); }, out GameObject orthoRow);
        orthoNote = Label(orthoRow.transform, "(not in first person / free cam)", 17f, FontStyles.Italic, new Color(1f, 0.8f, 0.4f), TextAlignmentOptions.MidlineRight);
        RectTransform nr = orthoNote.rectTransform;
        nr.anchorMin = Vector2.zero; nr.anchorMax = Vector2.one;
        nr.offsetMin = new Vector2(Width * 0.5f, 0f); nr.offsetMax = new Vector2(-(RowH + 20f), 0f);
        y += RowH + 14f;

        // --- Zoom slider (the normal view's zoom, or the orbit / top down camera's distance).
        zoomSlider = SliderRow(window.transform, y, "Zoom", 0.35f, 2f, v =>
        {
            PixelOrbitCamera orbit = PixelOrbitCamera.Instance;
            if (PixelOrbitCamera.Active && orbit != null) { orbit.DistanceFraction = v; PlayerPrefs.SetFloat(PrefOrbitZoom, v); }
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
        autoTick = TickRow(orbitSection.transform, oy, "Auto orbit", on => { if (PixelOrbitCamera.Instance != null) PixelOrbitCamera.Instance.AutoOrbit = on; PlayerPrefs.SetInt(PrefAuto, on ? 1 : 0); }, out _);
        oy += RowH + 6f;
        invertTick = TickRow(orbitSection.transform, oy, "Invert Ctrl + scroll zoom", on => { InvertScroll = on; PlayerPrefs.SetInt(PrefInvert, on ? 1 : 0); }, out _);
        oy += RowH + 6f;
        speedSlider = SliderRow(orbitSection.transform, oy, "Auto speed", 2f, 60f, v => { if (PixelOrbitCamera.Instance != null) PixelOrbitCamera.Instance.AutoSpeed = v; PlayerPrefs.SetFloat(PrefSpeed, v); }, out _, out speedValue);
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
