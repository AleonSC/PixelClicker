// ---- System notes (moved here from CLAUDE.md) ----
// PixelFeedback.cs
//   Give feedback (for testers): the pause menu's long green Give feedback button (right above Quit; no longer in Settings) or the
//   rebindable PixelAction.Feedback key (default F8; PixelKeys enum value 14, label 'Give feedback') opens a 1400x960 window (own
//   canvas 800, above the pause menu, PixelPop): a multi-line text box, Take screenshot (CaptureRoutine: hides this window's canvas
//   and the pause menu's (PixelPauseMenu.SetMenuVisible(false)) for a frame, WaitForEndOfFrame,
//   ScreenCapture.CaptureScreenshotAsTexture = the game without any menu; up to maxShots 4, selected with small numbered buttons in
//   the drawing area), a pencil (8 colours, fixed width pencilWidthFraction of the picture; strokes are drawn straight into the
//   texture's pixel array by PixelFeedbackCanvas pointer events), Undo (removes the last stroke and replays the rest over the clean
//   screenshot, maxStrokes), Remove shot, Open feedback folder, Cancel and Save feedback: everything is appended to ONE text file
//   <persistentDataPath>/Feedback/feedback.txt (a block per entry: date / time, game version, Unity version, platform, session
//   minutes, the text, the screenshot file names) and each (drawn) screenshot is saved next to it as feedback_<timestamp>_<n>.png.
//   While it is open the game is paused (restored on close; the pause menu already pauses) and PixelKeys.Typing reads true so the
//   game's keys do nothing; Escape closes it (PixelFeedback.Open / ClosedThisFrame keep the pause menu from also stepping back). Added
//   to the pause menu's object by PixelPauseMenu.BuildUI.
// ---------------------------------------------------
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// "Give feedback": a window for testers (Settings > Give feedback, or the rebindable Feedback key, default F8) with a text box, a Take screenshot button
/// (the shot is the game WITHOUT the menus / this window), a pencil with a few colours and an Undo button to scribble on the shots, and Save. Everything goes
/// into ONE text file, <c>&lt;persistentDataPath&gt;/Feedback/feedback.txt</c> (each entry: date, game version, the text, the screenshot file names); the
/// drawn screenshots are saved next to it as PNGs. While it is open the game is paused (restored on close) and the game's other keys are muted.
/// Added to the pause menu's object by <c>PixelPauseMenu.BuildUI</c>.
/// </summary>
public class PixelFeedback : MonoBehaviour
{
    [Header("Window")]
    [Tooltip("Title of the feedback window.")]
    [SerializeField] private string title = "Give feedback";
    [Tooltip("Grey hint text shown in the empty feedback box.")]
    [SerializeField] private string placeholder = "Type your feedback here: what you liked, what felt off, bugs you found...";
    [Tooltip("Canvas sorting order of the feedback window (higher = drawn over more of the UI).")]
    [SerializeField] private int sortingOrder = 800;

    [Header("Screenshots")]
    [Tooltip("Most screenshots one feedback entry can hold.")]
    [SerializeField] private int maxShots = 4;
    [Tooltip("Pencil width as a fraction of the screenshot's width.")]
    [SerializeField] private float pencilWidthFraction = 0.0035f;
    [Tooltip("Most pencil strokes kept per screenshot for Undo.")]
    [SerializeField] private int maxStrokes = 200;

    [Header("Saving")]
    [Tooltip("Folder (inside the game's data folder) the feedback file and screenshots are written to.")]
    [SerializeField] private string folderName = "Feedback";
    [Tooltip("Name of the single text file all feedback is appended to.")]
    [SerializeField] private string fileName = "feedback.txt";

    private static PixelFeedback instance;
    private static int closedFrame = -10;

    /// <summary>True while the window is open.</summary>
    public static bool Open => instance != null && instance.canvasRoot != null;

    /// <summary>True on the frame the window was just closed (so the same Escape press does not also step back in the pause menu).</summary>
    public static bool ClosedThisFrame => closedFrame == Time.frameCount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; closedFrame = -10; }

    private class Shot
    {
        public Texture2D tex;
        public Color32[] baseData, work;
        public readonly List<Stroke> strokes = new List<Stroke>();
    }

    private class Stroke { public Color32 color; public int radius; public readonly List<Vector2Int> points = new List<Vector2Int>(); }

    private readonly List<Shot> shots = new List<Shot>();
    private int selected = -1;
    private GameObject canvasRoot;
    private Canvas canvas;
    private TMP_InputField input;
    private RawImage view;
    private RectTransform viewRect, viewArea;
    private TMP_Text status, emptyHint;
    private readonly List<Button> shotButtons = new List<Button>();
    private readonly List<Image> swatches = new List<Image>();
    private Transform shotRow;
    private int colorIndex = 2;
    private Stroke current;
    private bool changedTime, busy;
    private float previousScale = 1f;
    private float closeAt = -1f;
    private TMP_FontAsset font;

    private static readonly Color32[] Palette =
    {
        new Color32(255, 255, 255, 255), new Color32(20, 20, 24, 255), new Color32(235, 50, 50, 255), new Color32(255, 150, 30, 255),
        new Color32(255, 225, 50, 255), new Color32(70, 210, 90, 255), new Color32(50, 170, 255, 255), new Color32(220, 80, 230, 255),
    };

    private void Awake() { instance = this; }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        CloseWindow(false);
    }

    /// <summary>Opens the feedback window (no-op if it is already open).</summary>
    public static void OpenWindow() { if (instance != null) instance.Show(); }

    private void Update()
    {
        if (!Open)
        {
            if (!PixelTitleScreen.Showing && PixelKeys.Pressed(PixelAction.Feedback)) Show();
            return;
        }

        if (closeAt > 0f && Time.unscaledTime >= closeAt) { CloseWindow(true); return; }
        if (!busy && EscapeDown()) CloseWindow(true);
    }

    private static bool EscapeDown()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    // ------------------------------------------------------------------
    // Window
    // ------------------------------------------------------------------

    private void Show()
    {
        if (Open) return;
        PixelClicker clicker = PixelFind.First<PixelClicker>();
        font = clicker != null ? clicker.UIFont : null;
        if (Time.timeScale > 0f) { previousScale = Time.timeScale; Time.timeScale = 0f; changedTime = true; }   // paused while the window is open (the pause menu has already done this)
        BuildWindow();
    }

    private void CloseWindow(bool restore)
    {
        if (canvasRoot != null) Destroy(canvasRoot);
        canvasRoot = null;
        foreach (Shot s in shots) if (s.tex != null) Destroy(s.tex);
        shots.Clear();
        selected = -1;
        current = null;
        busy = false;
        closeAt = -1f;
        if (restore) closedFrame = Time.frameCount;
        if (changedTime) { changedTime = false; Time.timeScale = previousScale; }
    }

    private Button AddButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, Color color, float fontSize, UnityEngine.Events.UnityAction onClick)
    {
        Button b = PixelUIKit.CreateButton(font, parent, name, label, size, color, Color.white, fontSize);
        RectTransform r = b.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = pos;
        b.onClick.AddListener(onClick);
        return b;
    }

    /// <summary>A button in a ROLE (its colour comes from PixelUITheme).</summary>
    private Button AddButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, ButtonRole role, float fontSize, UnityEngine.Events.UnityAction onClick)
    {
        Button b = AddButton(parent, name, label, pos, size, PixelUITheme.ColorOf(role), fontSize, onClick);
        PixelUIKit.SetRole(b, role);
        return b;
    }

    private void BuildWindow()
    {
        PixelUIKit.EnsureEventSystem();
        canvasRoot = PixelUIKit.CreateCanvas("PixelFeedback Canvas", sortingOrder, new Vector2(1920f, 1080f), true);
        canvas = canvasRoot.GetComponent<Canvas>();

        GameObject dim = new GameObject("Dim", typeof(RectTransform), typeof(Image));
        dim.transform.SetParent(canvasRoot.transform, false);
        dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        PixelUIKit.Stretch(dim.GetComponent<RectTransform>());

        GameObject win = new GameObject("Window", typeof(RectTransform), typeof(Image), typeof(PixelPop));
        win.transform.SetParent(canvasRoot.transform, false);
        PixelUIKit.StyleWindow(win.GetComponent<Image>(), new Color(0.07f, 0.09f, 0.14f, 0.98f));
        RectTransform wr = win.GetComponent<RectTransform>();
        wr.anchorMin = wr.anchorMax = wr.pivot = new Vector2(0.5f, 0.5f);
        wr.sizeDelta = new Vector2(1400f, 960f);

        TMP_Text t = PixelUIKit.CreateText(font, win.transform, "Title", title, 44f, TextAlignmentOptions.Center, FontStyles.Bold, Color.white);
        PixelUIKit.Caps(t);
        RectTransform tr = t.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f); tr.pivot = new Vector2(0.5f, 1f);
        tr.sizeDelta = new Vector2(-240f, 64f); tr.anchoredPosition = new Vector2(0f, -14f);

        Button close = AddButton(win.transform, "Close", "X", new Vector2(1400f - 40f - 56f, -14f), new Vector2(56f, 56f), ButtonRole.Neutral, 30f, () => CloseWindow(true));
        close.GetComponent<RectTransform>().anchoredPosition = new Vector2(1400f - 14f - 56f, -14f);

        // The text box.
        input = PixelUIKit.CreateInputField(font, win.transform, "Feedback Text", new Vector2(1320f, 130f), new Color(0.03f, 0.04f, 0.07f, 1f), Color.white, 26f, placeholder);
        input.lineType = TMP_InputField.LineType.MultiLineNewline;
        input.textComponent.alignment = TextAlignmentOptions.TopLeft;
        if (input.placeholder is TMP_Text ph) ph.alignment = TextAlignmentOptions.TopLeft;
        RectTransform ir = input.GetComponent<RectTransform>();
        ir.anchorMin = ir.anchorMax = ir.pivot = new Vector2(0f, 1f);
        ir.anchoredPosition = new Vector2(40f, -86f);

        // Tool row: screenshot, undo, remove, pencil colours.
        float y = -232f;
        AddButton(win.transform, "Take Screenshot", "Take screenshot", new Vector2(40f, y), new Vector2(300f, 58f), ButtonRole.Primary, 26f, TakeShotClicked);
        AddButton(win.transform, "Undo", "Undo", new Vector2(356f, y), new Vector2(130f, 58f), ButtonRole.Neutral, 26f, Undo);
        AddButton(win.transform, "Remove Shot", "Remove shot", new Vector2(502f, y), new Vector2(210f, 58f), ButtonRole.Neutral, 24f, RemoveShot);
        TMP_Text pencil = PixelUIKit.CreateText(font, win.transform, "Pencil Label", "Pencil", 26f, TextAlignmentOptions.MidlineRight, FontStyles.Bold, new Color(1f, 0.88f, 0.5f, 1f));
        PixelUIKit.Caps(pencil);
        RectTransform pr = pencil.rectTransform;
        pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(0f, 1f);
        pr.sizeDelta = new Vector2(130f, 58f); pr.anchoredPosition = new Vector2(740f, y);
        swatches.Clear();
        for (int i = 0; i < Palette.Length; i++)
        {
            int index = i;
            // (stays a plain colour, role Custom: a swatch shows its pencil colour)
            Button sw = AddButton(win.transform, "Colour " + i, "", new Vector2(884f + i * 56f, y - 5f), new Vector2(48f, 48f), Palette[i], 20f, () => { colorIndex = index; RefreshSwatches(); });
            swatches.Add(sw.GetComponent<Image>());
            Outline ring = sw.gameObject.AddComponent<Outline>();
            ring.effectColor = new Color(1f, 1f, 1f, 0f);
            ring.effectDistance = new Vector2(4f, 4f);
        }
        RefreshSwatches();

        // The drawing area.
        GameObject area = new GameObject("Draw Area", typeof(RectTransform), typeof(Image));
        area.transform.SetParent(win.transform, false);
        area.GetComponent<Image>().color = new Color(0.02f, 0.03f, 0.05f, 1f);
        PixelUIKit.StyleBox(area.GetComponent<Image>(), true);
        viewArea = area.GetComponent<RectTransform>();
        viewArea.anchorMin = viewArea.anchorMax = viewArea.pivot = new Vector2(0f, 1f);
        viewArea.sizeDelta = new Vector2(1320f, 560f);
        viewArea.anchoredPosition = new Vector2(40f, -304f);

        GameObject v = new GameObject("Screenshot", typeof(RectTransform), typeof(RawImage), typeof(PixelFeedbackCanvas));
        v.transform.SetParent(area.transform, false);
        view = v.GetComponent<RawImage>();
        viewRect = v.GetComponent<RectTransform>();
        viewRect.anchorMin = viewRect.anchorMax = viewRect.pivot = new Vector2(0.5f, 0.5f);
        PixelFeedbackCanvas pen = v.GetComponent<PixelFeedbackCanvas>();
        pen.onDown = PenDown; pen.onMove = PenMove; pen.onUp = PenUp;
        view.enabled = false;

        emptyHint = PixelUIKit.CreateText(font, area.transform, "Hint", "Press 'Take screenshot' to grab the game (without this window), then scribble on it with the pencil.", 26f,
                                          TextAlignmentOptions.Center, FontStyles.Italic, new Color(1f, 1f, 1f, 0.45f));
        emptyHint.raycastTarget = false;
        PixelUIKit.Stretch(emptyHint.rectTransform);
        emptyHint.rectTransform.offsetMin = new Vector2(80f, 0f); emptyHint.rectTransform.offsetMax = new Vector2(-80f, 0f);

        // Shot selector (1..n) in the corner of the drawing area.
        GameObject row = new GameObject("Shots", typeof(RectTransform));
        row.transform.SetParent(area.transform, false);
        shotRow = row.transform;
        RectTransform rr = row.GetComponent<RectTransform>();
        rr.anchorMin = rr.anchorMax = rr.pivot = new Vector2(0f, 1f);
        rr.anchoredPosition = new Vector2(12f, -12f);
        rr.sizeDelta = new Vector2(400f, 52f);
        shotButtons.Clear();

        // Bottom row.
        float by = -884f;
        AddButton(win.transform, "Open Folder", "Open feedback folder", new Vector2(40f, by), new Vector2(330f, 56f), ButtonRole.Neutral, 22f, OpenFolder);
        status = PixelUIKit.CreateText(font, win.transform, "Status", "", 24f, TextAlignmentOptions.Midline, FontStyles.Bold, new Color(1f, 0.88f, 0.5f, 1f));
        status.raycastTarget = false;
        RectTransform sr = status.rectTransform;
        sr.anchorMin = sr.anchorMax = sr.pivot = new Vector2(0f, 1f);
        sr.sizeDelta = new Vector2(400f, 56f); sr.anchoredPosition = new Vector2(390f, by);
        AddButton(win.transform, "Cancel", "Cancel", new Vector2(830f, by), new Vector2(240f, 56f), ButtonRole.Neutral, 26f, () => CloseWindow(true));
        AddButton(win.transform, "Save", "Save feedback", new Vector2(1090f, by), new Vector2(270f, 56f), ButtonRole.Success, 26f, Save);

        RefreshShotButtons();
        input.ActivateInputField();
    }

    private void RefreshSwatches()
    {
        for (int i = 0; i < swatches.Count; i++)
        {
            Outline o = swatches[i].GetComponent<Outline>();
            if (o != null) o.effectColor = new Color(1f, 1f, 1f, i == colorIndex ? 1f : 0f);
            swatches[i].rectTransform.localScale = Vector3.one * (i == colorIndex ? 1.12f : 1f);
        }
    }

    private void RefreshShotButtons()
    {
        foreach (Button b in shotButtons) if (b != null) Destroy(b.gameObject);
        shotButtons.Clear();
        if (emptyHint != null) emptyHint.gameObject.SetActive(shots.Count == 0);
        for (int i = 0; i < shots.Count; i++)
        {
            int index = i;
            Button b = PixelUIKit.CreateButton(font, shotRow, "Shot " + (i + 1), (i + 1).ToString(), new Vector2(56f, 46f),
                                               i == selected ? ButtonRole.Selected : ButtonRole.Neutral, 24f);

            RectTransform r = b.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
            r.anchoredPosition = new Vector2(i * 62f, 0f);
            b.onClick.AddListener(() => { selected = index; ShowSelected(); RefreshShotButtons(); });
            shotButtons.Add(b);
        }
    }

    private void ShowSelected()
    {
        if (view == null) return;
        if (selected < 0 || selected >= shots.Count) { view.enabled = false; return; }
        Shot s = shots[selected];
        view.texture = s.tex;
        view.enabled = true;
        // Fit the picture inside the area, keeping its shape.
        Vector2 box = viewArea.sizeDelta - new Vector2(16f, 16f);
        float scale = Mathf.Min(box.x / s.tex.width, box.y / s.tex.height);
        viewRect.sizeDelta = new Vector2(s.tex.width * scale, s.tex.height * scale);
        viewRect.anchoredPosition = Vector2.zero;
    }

    // ------------------------------------------------------------------
    // Screenshots
    // ------------------------------------------------------------------

    private void TakeShotClicked()
    {
        if (busy) return;
        if (shots.Count >= Mathf.Max(1, maxShots)) { SetStatus("That is the most screenshots for one entry."); return; }
        StartCoroutine(CaptureRoutine());
    }

    private IEnumerator CaptureRoutine()
    {
        busy = true;
        // Hide this window and the pause menu for a frame so the picture is just the game.
        if (canvas != null) canvas.enabled = false;
        PixelPauseMenu.SetMenuVisible(false);
        yield return null;
        yield return new WaitForEndOfFrame();
        Texture2D grabbed = null;
        try { grabbed = ScreenCapture.CaptureScreenshotAsTexture(); }
        catch (Exception e) { Debug.LogWarning("PixelFeedback: screenshot failed (" + e.Message + ")", this); }
        PixelPauseMenu.SetMenuVisible(true);
        if (canvas != null) canvas.enabled = true;
        busy = false;
        if (grabbed == null) { SetStatus("The screenshot failed."); yield break; }

        Shot shot = new Shot();
        shot.baseData = grabbed.GetPixels32();
        for (int i = 0; i < shot.baseData.Length; i++) shot.baseData[i].a = 255;
        shot.tex = new Texture2D(grabbed.width, grabbed.height, TextureFormat.RGBA32, false) { name = "Feedback Shot", filterMode = FilterMode.Bilinear };
        Destroy(grabbed);
        shot.work = new Color32[shot.baseData.Length];
        Array.Copy(shot.baseData, shot.work, shot.baseData.Length);
        shot.tex.SetPixels32(shot.work);
        shot.tex.Apply(false);
        shots.Add(shot);
        selected = shots.Count - 1;
        ShowSelected();
        RefreshShotButtons();
        SetStatus("Screenshot " + shots.Count + " added.");
    }

    private void RemoveShot()
    {
        if (selected < 0 || selected >= shots.Count) return;
        if (shots[selected].tex != null) Destroy(shots[selected].tex);
        shots.RemoveAt(selected);
        selected = shots.Count > 0 ? Mathf.Clamp(selected, 0, shots.Count - 1) : -1;
        ShowSelected();
        RefreshShotButtons();
    }

    // ------------------------------------------------------------------
    // Pencil
    // ------------------------------------------------------------------

    private bool ToPixel(Vector2 screen, out Vector2Int pixel)
    {
        pixel = Vector2Int.zero;
        if (selected < 0 || selected >= shots.Count) return false;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(viewRect, screen, null, out Vector2 local)) return false;
        Rect r = viewRect.rect;
        Shot s = shots[selected];
        float u = (local.x - r.xMin) / r.width, v = (local.y - r.yMin) / r.height;
        pixel = new Vector2Int(Mathf.Clamp(Mathf.RoundToInt(u * (s.tex.width - 1)), 0, s.tex.width - 1), Mathf.Clamp(Mathf.RoundToInt(v * (s.tex.height - 1)), 0, s.tex.height - 1));
        return true;
    }

    private void PenDown(Vector2 screen)
    {
        if (!ToPixel(screen, out Vector2Int p)) return;
        Shot s = shots[selected];
        current = new Stroke { color = Palette[colorIndex], radius = Mathf.Max(1, Mathf.RoundToInt(s.tex.width * pencilWidthFraction)) };
        current.points.Add(p);
        if (s.strokes.Count >= maxStrokes) s.strokes.RemoveAt(0);   // the oldest stroke can no longer be undone (it stays baked into the picture)
        s.strokes.Add(current);
        Stamp(s.work, s.tex.width, s.tex.height, p, current.color, current.radius);
        Upload(s);
    }

    private void PenMove(Vector2 screen)
    {
        if (current == null || !ToPixel(screen, out Vector2Int p)) return;
        Shot s = shots[selected];
        Vector2Int last = current.points[current.points.Count - 1];
        if (p == last) return;
        current.points.Add(p);
        Line(s.work, s.tex.width, s.tex.height, last, p, current.color, current.radius);
        Upload(s);
    }

    private void PenUp() { current = null; }

    private void Undo()
    {
        if (selected < 0 || selected >= shots.Count) return;
        Shot s = shots[selected];
        if (s.strokes.Count == 0) { SetStatus("Nothing to undo."); return; }
        s.strokes.RemoveAt(s.strokes.Count - 1);
        // Redraw: the clean screenshot plus every stroke that is left.
        Array.Copy(s.baseData, s.work, s.baseData.Length);
        foreach (Stroke st in s.strokes)
        {
            Stamp(s.work, s.tex.width, s.tex.height, st.points[0], st.color, st.radius);
            for (int i = 1; i < st.points.Count; i++) Line(s.work, s.tex.width, s.tex.height, st.points[i - 1], st.points[i], st.color, st.radius);
        }
        Upload(s);
    }

    private static void Upload(Shot s) { s.tex.SetPixels32(s.work); s.tex.Apply(false); }

    private static void Stamp(Color32[] px, int w, int h, Vector2Int c, Color32 color, int radius)
    {
        int r2 = radius * radius;
        for (int dy = -radius; dy <= radius; dy++)
        {
            int y = c.y + dy;
            if (y < 0 || y >= h) continue;
            for (int dx = -radius; dx <= radius; dx++)
            {
                int x = c.x + dx;
                if (x < 0 || x >= w || dx * dx + dy * dy > r2) continue;
                px[y * w + x] = color;
            }
        }
    }

    private static void Line(Color32[] px, int w, int h, Vector2Int a, Vector2Int b, Color32 color, int radius)
    {
        int steps = Mathf.Max(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));
        int every = Mathf.Max(1, radius / 2);   // stamps overlap, so the line has no gaps
        for (int i = 0; i <= steps; i += every)
        {
            float t = steps == 0 ? 0f : (float)i / steps;
            Stamp(px, w, h, new Vector2Int(Mathf.RoundToInt(Mathf.Lerp(a.x, b.x, t)), Mathf.RoundToInt(Mathf.Lerp(a.y, b.y, t))), color, radius);
        }
        Stamp(px, w, h, b, color, radius);
    }

    // ------------------------------------------------------------------
    // Saving
    // ------------------------------------------------------------------

    private string Folder => Path.Combine(Application.persistentDataPath, folderName);

    private void SetStatus(string text) { if (status != null) status.text = text; }

    private void OpenFolder()
    {
        try
        {
            Directory.CreateDirectory(Folder);
#if UNITY_EDITOR
            UnityEditor.EditorUtility.RevealInFinder(Folder);
#else
            string path = Folder.Replace("\\", "/");
            Application.OpenURL((path.StartsWith("/") ? "file://" : "file:///") + path);
#endif
        }
        catch (Exception e) { SetStatus("Could not open the folder."); Debug.LogWarning("PixelFeedback: " + e.Message, this); }
    }

    private void Save()
    {
        string text = input != null ? input.text.Trim() : "";
        if (text.Length == 0 && shots.Count == 0) { SetStatus("Type something or take a screenshot first."); return; }
        try
        {
            Directory.CreateDirectory(Folder);
            DateTime now = DateTime.Now;
            string stamp = now.ToString("yyyyMMdd_HHmmss");
            List<string> files = new List<string>();
            for (int i = 0; i < shots.Count; i++)
            {
                string name = "feedback_" + stamp + "_" + (i + 1) + ".png";
                File.WriteAllBytes(Path.Combine(Folder, name), shots[i].tex.EncodeToPNG());
                files.Add(name);
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("==================== FEEDBACK " + now.ToString("yyyy-MM-dd HH:mm:ss") + " ====================");
            sb.AppendLine("Game version: " + Application.version + "   Unity " + Application.unityVersion + "   " + Application.platform);
            sb.AppendLine("Session time: " + Mathf.RoundToInt(Time.realtimeSinceStartup / 60f) + " min");
            sb.AppendLine();
            sb.AppendLine(text.Length > 0 ? text : "(no text)");
            if (files.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Screenshots (in the same folder): " + string.Join(", ", files));
            }
            sb.AppendLine();
            sb.AppendLine();
            File.AppendAllText(Path.Combine(Folder, fileName), sb.ToString(), Encoding.UTF8);
            SetStatus("Saved - thank you!");
            closeAt = Time.unscaledTime + 1.3f;
        }
        catch (Exception e)
        {
            SetStatus("Could not save: " + e.Message);
            Debug.LogWarning("PixelFeedback: " + e, this);
        }
    }
}

/// <summary>Forwards pointer presses and drags on the screenshot to the feedback window's pencil.</summary>
public class PixelFeedbackCanvas : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public System.Action<Vector2> onDown, onMove;
    public System.Action onUp;

    public void OnPointerDown(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) onDown?.Invoke(e.position); }
    public void OnDrag(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) onMove?.Invoke(e.position); }
    public void OnPointerUp(PointerEventData e) { onUp?.Invoke(); }
}
