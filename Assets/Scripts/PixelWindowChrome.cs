// ---- System notes ----
// PixelWindowChrome.cs
//   Reusable window behaviour for a code-built window (first user: the Dev Tools panel; My Window, the Mock up window and the Inspector each still carry their own copy of
//   this logic - folding them into this one is the obvious cleanup). PixelWindowChrome.Attach(windowRect, font, prefKey, headerHeight, firstButtonRight, buttonTop, buttonSize, ...)
//   on a window whose RectTransform is anchored + pivoted at the TOP LEFT of a full-screen canvas gives it:
//   * DRAG by its header strip (a transparent handle behind the header's buttons), kept inside the playfield: between the black bars (PixelHud.BarHeight) and the screen sides.
//   * MINIMIZE (button '-'): everything except the header, the handle and the chrome buttons is hidden and the window shrinks to the header (restores exactly what was showing).
//   * PIN (button 'P', green while pinned): Pinned is a flag the owner reads (Dev Tools: a pinned window ignores Escape and doesn't count as an open window, so the game stays playable).
//   * DOCK to the left / right edge: drag it against a side (a pale strip shows where) and it docks at that height with only a sliver showing; it slides out while the mouse is
//     near it or you are typing in it; an undock arrow (replaces the pin button while docked) puts it back. Docked windows can't be dragged until undocked.
//   Saved in PlayerPrefs prefKey + Pin / Min / Dock / DockY / X / Y (not part of the game save).
// ---------------------------------------------------
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PixelWindowChrome : MonoBehaviour
{
    private const float EdgeSnap = 70f, DockSliver = 36f, EdgeMargin = 14f, SlideSeconds = 0.22f;

    public bool Pinned { get; private set; }
    public bool Minimized => minimized;
    public bool Docked => dockSide != 0;

    private RectTransform window, canvasRect, hint;
    private Canvas canvas;
    private string prefKey;
    private float headerHeight, restoreHeight;
    private bool minimized, placed;
    private int dockSide;               // 0 = free, -1 = left edge, 1 = right edge
    private float dockY, dockSlide = 1f;
    private PixelPopoutDrag drag;
    private Button pinButton, undockButton;
    private Image pinImage;
    private Vector2 lastCanvasSize;
    private readonly List<GameObject> keep = new List<GameObject>(), hidden = new List<GameObject>();
    private static readonly Color PinOn = new Color(0.2f, 0.6f, 0.3f, 1f);
    private Color buttonColorOff;

    /// <summary>Adds the chrome to a top-left anchored window. 'firstButtonRight' = distance from the window's right edge to the right edge of the first (Minimize) button.</summary>
    public static PixelWindowChrome Attach(RectTransform window, TMP_FontAsset font, string prefKey, float headerHeight, float firstButtonRight, float buttonTop,
                                           float buttonSize, Color buttonColor, Color labelColor, float labelSize)
    {
        PixelWindowChrome c = window.gameObject.AddComponent<PixelWindowChrome>();
        c.Build(window, font, prefKey, headerHeight, firstButtonRight, buttonTop, buttonSize, buttonColor, labelColor, labelSize);
        return c;
    }

    /// <summary>Anything else that must stay visible while minimized (the owner's own header buttons).</summary>
    public void Keep(GameObject go) { if (go != null && !keep.Contains(go)) keep.Add(go); }

    private static int Get(string k, int d) => PlayerPrefs.GetInt(k, d);

    private void Build(RectTransform win, TMP_FontAsset font, string key, float header, float right, float top, float size, Color buttonColor, Color labelColor, float labelSize)
    {
        window = win;
        prefKey = key;
        headerHeight = header;
        buttonColorOff = buttonColor;
        canvas = win.GetComponentInParent<Canvas>();
        canvasRect = canvas != null ? canvas.rootCanvas.GetComponent<RectTransform>() : null;

        // The drag handle: a transparent strip over the header, behind everything else.
        GameObject handle = new GameObject("Chrome Drag Handle", typeof(RectTransform), typeof(Image), typeof(PixelPopoutDrag));
        handle.transform.SetParent(win, false);
        handle.transform.SetAsFirstSibling();
        handle.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0f);
        RectTransform hr = handle.GetComponent<RectTransform>();
        hr.anchorMin = new Vector2(0f, 1f); hr.anchorMax = new Vector2(1f, 1f); hr.pivot = new Vector2(0.5f, 1f);
        hr.sizeDelta = new Vector2(0f, header);
        hr.anchoredPosition = Vector2.zero;
        drag = handle.GetComponent<PixelPopoutDrag>();
        drag.target = win;
        drag.canvas = canvas;
        drag.onMoved = () => { Clamp(); UpdateHint(); };
        drag.onEnd = () => { TryDock(); UpdateHint(); Save(); };
        keep.Add(handle);

        Button min = MakeButton(font, "Chrome Minimize", "-", right, top, size, buttonColor, labelColor, labelSize);
        min.onClick.AddListener(() => SetMinimized(!minimized));
        pinButton = MakeButton(font, "Chrome Pin", "P", right + size + 8f, top, size, buttonColor, labelColor, labelSize);
        pinImage = pinButton.GetComponent<Image>();
        pinButton.onClick.AddListener(() => { Pinned = !Pinned; PlayerPrefs.SetInt(prefKey + ".Pin", Pinned ? 1 : 0); ApplyState(); });
        undockButton = MakeButton(font, "Chrome Undock", ">", right + size + 8f, top, size, buttonColor, labelColor, labelSize);
        PixelUIKit.UseArrowGlyph(undockButton, false);
        undockButton.onClick.AddListener(Undock);
        keep.Add(min.gameObject); keep.Add(pinButton.gameObject); keep.Add(undockButton.gameObject);

        GameObject hintGo = new GameObject("Chrome Dock Hint", typeof(RectTransform), typeof(Image));
        hintGo.transform.SetParent(win.parent, false);
        hintGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.2f);
        hintGo.GetComponent<Image>().raycastTarget = false;
        hint = hintGo.GetComponent<RectTransform>();
        hintGo.SetActive(false);

        Pinned = Get(prefKey + ".Pin", 0) != 0;
        dockSide = Mathf.Clamp(Get(prefKey + ".Dock", 0), -1, 1);
        dockY = PlayerPrefs.GetFloat(prefKey + ".DockY", -120f);
        restoreHeight = win.sizeDelta.y;
        if (PlayerPrefs.HasKey(prefKey + ".X")) { win.anchoredPosition = new Vector2(PlayerPrefs.GetFloat(prefKey + ".X"), PlayerPrefs.GetFloat(prefKey + ".Y")); placed = true; }
        if (Get(prefKey + ".Min", 0) != 0) SetMinimized(true, false);
        ApplyState();
    }

    private Button MakeButton(TMP_FontAsset font, string name, string label, float right, float top, float size, Color color, Color labelColor, float labelSize)
    {
        Button b = PixelUIKit.CreateButton(font, window, name, label, new Vector2(size, size), color, labelColor, labelSize);
        RectTransform r = b.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(1f, 1f);
        r.sizeDelta = new Vector2(size, size);
        r.anchoredPosition = new Vector2(-right, -top);
        return b;
    }

    private void Save()
    {
        PlayerPrefs.SetFloat(prefKey + ".X", window.anchoredPosition.x);
        PlayerPrefs.SetFloat(prefKey + ".Y", window.anchoredPosition.y);
        PlayerPrefs.SetInt(prefKey + ".Dock", dockSide);
        PlayerPrefs.SetFloat(prefKey + ".DockY", dockY);
    }

    private float BarInset() => (PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 84f) + EdgeMargin;

    /// <summary>Keeps the window between the black bars and the screen sides (a docked window only slides sideways).</summary>
    private void Clamp()
    {
        if (canvasRect == null) return;
        Vector2 cs = canvasRect.rect.size;
        if (cs.x < 100f || cs.y < 100f) return;
        Vector2 p = window.anchoredPosition, sz = window.sizeDelta;
        float inset = BarInset();
        if (dockSide == 0) p.x = Mathf.Clamp(p.x, EdgeMargin, Mathf.Max(EdgeMargin, cs.x - sz.x - EdgeMargin));
        float maxY = -inset, minY = -(cs.y - inset - sz.y);
        p.y = Mathf.Clamp(p.y, Mathf.Min(minY, maxY), maxY);
        window.anchoredPosition = p;
        if (dockSide != 0) dockY = p.y;
    }

    private void SetMinimized(bool on, bool save = true)
    {
        if (on == minimized) return;
        if (on) restoreHeight = window.sizeDelta.y;
        minimized = on;
        if (on)
        {
            hidden.Clear();
            foreach (Transform child in window)
                if (child.gameObject.activeSelf && !keep.Contains(child.gameObject)) { hidden.Add(child.gameObject); child.gameObject.SetActive(false); }
        }
        else
        {
            foreach (GameObject g in hidden) if (g != null) g.SetActive(true);
            hidden.Clear();
        }
        window.sizeDelta = new Vector2(window.sizeDelta.x, minimized ? headerHeight : restoreHeight);
        Clamp();
        if (save) { PlayerPrefs.SetInt(prefKey + ".Min", minimized ? 1 : 0); Save(); }
    }

    private void TryDock()
    {
        if (dockSide != 0 || canvasRect == null) return;
        Vector2 cs = canvasRect.rect.size;
        float left = window.anchoredPosition.x, right = left + window.sizeDelta.x;
        int side = left < EdgeSnap ? -1 : cs.x - right < EdgeSnap ? 1 : 0;
        if (side == 0) return;
        dockSide = side;
        dockY = window.anchoredPosition.y;
        dockSlide = 1f;
        ApplyState();
        PixelAudio.Play("ui_click");
    }

    private void Undock()
    {
        if (dockSide == 0 || canvasRect == null) return;
        Vector2 cs = canvasRect.rect.size;
        int side = dockSide;
        dockSide = 0;
        window.anchoredPosition = new Vector2(side < 0 ? EdgeMargin : cs.x - window.sizeDelta.x - EdgeMargin, dockY);
        Clamp();
        ApplyState();
        Save();
        PixelAudio.Play("ui_click");
    }

    private void ApplyState()
    {
        if (undockButton != null)
        {
            undockButton.gameObject.SetActive(dockSide != 0);
            undockButton.GetComponent<RectTransform>().localRotation = Quaternion.Euler(0f, 0f, dockSide > 0 ? 180f : 0f);
        }
        if (pinButton != null) pinButton.gameObject.SetActive(dockSide == 0);
        if (pinImage != null) pinImage.color = Pinned ? PinOn : buttonColorOff;
        if (drag != null) drag.locked = dockSide != 0;
    }

    private void UpdateHint()
    {
        if (hint == null || canvasRect == null) return;
        bool dragging = PixelInput.LeftHeld() && dockSide == 0;
        Vector2 cs = canvasRect.rect.size;
        float left = window.anchoredPosition.x, right = left + window.sizeDelta.x;
        int side = !dragging ? 0 : left < EdgeSnap ? -1 : cs.x - right < EdgeSnap ? 1 : 0;
        hint.gameObject.SetActive(side != 0);
        if (side == 0) return;
        float inset = BarInset() - EdgeMargin;
        hint.anchorMin = new Vector2(side < 0 ? 0f : 1f, 0f);
        hint.anchorMax = new Vector2(side < 0 ? 0f : 1f, 1f);
        hint.pivot = new Vector2(side < 0 ? 0f : 1f, 0.5f);
        hint.sizeDelta = new Vector2(16f, -2f * inset);
        hint.anchoredPosition = Vector2.zero;
    }

    private bool TypingInside()
    {
        GameObject sel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        return sel != null && sel.transform.IsChildOf(window) && sel.GetComponent<TMP_InputField>() != null;
    }

    private void OnEnable() { dockSlide = 1f; }

    private void OnDisable() { if (hint != null) hint.gameObject.SetActive(false); }

    private void Update()
    {
        if (canvasRect == null) return;
        Vector2 cs = canvasRect.rect.size;
        if (cs.x < 100f || cs.y < 100f) return;
        if (!placed)
        {
            placed = true;   // first time ever: centre it
            window.anchoredPosition = new Vector2((cs.x - window.sizeDelta.x) * 0.5f, -(cs.y - window.sizeDelta.y) * 0.5f);
            Clamp();
        }
        else if (cs != lastCanvasSize) Clamp();
        lastCanvasSize = cs;

        if (dockSide == 0) { UpdateHint(); return; }
        float w = window.sizeDelta.x, h = window.sizeDelta.y;
        bool near = TypingInside();
        if (!near && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, PixelInput.PointerPosition(), null, out Vector2 local))
        {
            float px = local.x + cs.x * 0.5f, py = cs.y * 0.5f - local.y;   // from the top left, y down
            float edge = dockSide < 0 ? px : cs.x - px;
            float topY = -dockY;
            near = edge <= DockSliver + dockSlide * w && py >= topY - 20f && py <= topY + h + 20f;
        }
        dockSlide = Mathf.MoveTowards(dockSlide, near ? 1f : 0f, Time.unscaledDeltaTime / SlideSeconds);
        float e = Mathf.SmoothStep(0f, 1f, dockSlide);
        float shownX = dockSide < 0 ? EdgeMargin * 0.5f : cs.x - w - EdgeMargin * 0.5f;
        float hiddenX = dockSide < 0 ? -(w - DockSliver) : cs.x - DockSliver;
        window.anchoredPosition = new Vector2(Mathf.Lerp(hiddenX, shownX, e), dockY);
    }
}
