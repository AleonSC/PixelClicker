using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Pause menu for Pixel Clicker.
///
/// Press the pause key (Escape by default) or the on-screen Pause button to freeze the game
/// (Time.timeScale = 0) and open a menu with Resume, Restart and Quit. Press the key again or
/// Resume to carry on. Clicks on the cube are ignored while paused.
///
/// Add it to any GameObject. The UI builds itself at runtime, in the same style as the other panels.
/// </summary>
public class PixelPauseMenu : MonoBehaviour
{
    public enum PauseKey { Escape, P, Tab, Backspace, None }

    public enum ButtonAnchor { TopLeft, TopCenter, TopRight, BottomLeft, BottomCenter, BottomRight }

    [Header("References")]
    [Tooltip("Used only to share the game's UI font. Found automatically if left empty.")]
    [SerializeField] private PixelClicker clicker;

    [Tooltip("Save / load component used by the Save, Load and Restart buttons. Found automatically (or added) if left empty.")]
    [SerializeField] private PixelSaveGame saveGame;

    [Tooltip("Play statistics shown in the Stats section. Found automatically (or added) if left empty.")]
    [SerializeField] private PixelStats stats;

    [Tooltip("Fallback font, used only when the PixelClicker's 'UI Font' is empty. Empty = TextMeshPro default font.")]
    [SerializeField] private TMP_FontAsset font;

    [Header("Pausing")]
    [Tooltip("Key that opens and closes the menu.")]
    [SerializeField] private PauseKey pauseKey = PauseKey.Escape;

    [Tooltip("Also silence all audio while paused.")]
    [SerializeField] private bool pauseAudio = false;

    [Header("Pause Button")]
    [Tooltip("Show a Pause button on screen.")]
    [SerializeField] private bool showPauseButton = true;

    [Tooltip("Where the Pause button sits.")]
    [SerializeField] private ButtonAnchor buttonAnchor = ButtonAnchor.BottomCenter;

    [Tooltip("Distance of the button from the screen edge (canvas units).")]
    [SerializeField] private Vector2 buttonMargin = new Vector2(30f, 30f);

    [Tooltip("Size of the Pause button.")]
    [SerializeField] private Vector2 buttonSize = new Vector2(200f, 70f);

    [Tooltip("Text on the Pause button.")]
    [SerializeField] private string pauseButtonText = "Pause";

    [Tooltip("Pause button text size.")]
    [SerializeField] private float buttonFontSize = 34f;

    [Tooltip("Pause button colour.")]
    [SerializeField] private Color pauseButtonColor = new Color(0.35f, 0.35f, 0.42f, 1f);

    [Header("Menu")]
    [Tooltip("Title of the menu.")]
    [SerializeField] private string menuTitle = "Paused";

    [Tooltip("Resume button text.")]
    [SerializeField] private string resumeText = "Resume";

    [Tooltip("Restart button text.")]
    [SerializeField] private string restartText = "Restart";

    [Tooltip("Quit button text.")]
    [SerializeField] private string quitText = "Quit";

    [Tooltip("Add a Pixel Save Game component at startup if the scene has none.")]
    [SerializeField] private bool addSaveGameIfMissing = true;

    [Tooltip("Show the Save and Load buttons.")]
    [SerializeField] private bool showSaveLoad = true;

    [Tooltip("Save button text.")]
    [SerializeField] private string saveText = "Save";

    [Tooltip("Load button text.")]
    [SerializeField] private string loadText = "Load";

    [Tooltip("Restart also deletes the save file, so it starts a brand-new game. Off = Restart reloads the scene and then loads your save again.")]
    [SerializeField] private bool restartDeletesSave = true;

    [Header("Stats and Settings")]
    [Tooltip("Show the Stats button.")]
    [SerializeField] private bool showStats = true;

    [Tooltip("Show the Settings button.")]
    [SerializeField] private bool showSettings = true;

    [Tooltip("Stats button text.")]
    [SerializeField] private string statsText = "Stats";

    [Tooltip("Settings button text.")]
    [SerializeField] private string settingsText = "Settings";

    [Tooltip("Text on the Back button of the Stats and Settings sections.")]
    [SerializeField] private string backText = "Back";

    [Tooltip("Title of the Stats section.")]
    [SerializeField] private string statsTitle = "Stats";

    [Tooltip("Title of the Settings section.")]
    [SerializeField] private string settingsTitle = "Settings";

    [Tooltip("Label of the cube rotation tick box (accessibility).")]
    [SerializeField] private string rotationLabel = "Cube rotation";

    [Tooltip("Label of the cube pulsing tick box (accessibility).")]
    [SerializeField] private string pulsingLabel = "Cube pulsing";

    [Tooltip("Label of the tick box that decides whether the pause menu freezes the game.")]
    [SerializeField] private string pauseStopsLabel = "Pausing stops the game";

    [Tooltip("Label of the tick box that keeps the game running while you are alt-tabbed.")]
    [SerializeField] private string runInBackgroundLabel = "Run when alt-tabbed";

    [Tooltip("Label of the total clicks stat.")]
    [SerializeField] private string totalClicksLabel = "Total clicks";

    [Tooltip("Label of the manual clicks line.")]
    [SerializeField] private string manualClicksLabel = "   Your clicks";

    [Tooltip("Label of the auto clicks line.")]
    [SerializeField] private string autoClicksLabel = "   Auto clicker";

    [Tooltip("Label of the time played stat.")]
    [SerializeField] private string timePlayedLabel = "Time played";

    [Tooltip("Label of the pixels spent stat.")]
    [SerializeField] private string pixelsSpentLabel = "Pixels spent";

    [Tooltip("Height of each stat / setting row.")]
    [SerializeField] private float rowHeight = 64f;

    [Tooltip("Text size of stat / setting rows.")]
    [SerializeField] private float rowFontSize = 34f;

    [Tooltip("Colour of the stat numbers.")]
    [SerializeField] private Color statValueColor = new Color(1f, 0.92f, 0.5f, 1f);

    [Tooltip("Size of a tick box.")]
    [SerializeField] private float tickBoxSize = 52f;

    [Tooltip("Colour of an empty tick box.")]
    [SerializeField] private Color tickBoxColor = new Color(0.25f, 0.25f, 0.3f, 1f);

    [Tooltip("Colour of the tick inside a ticked box.")]
    [SerializeField] private Color tickColor = new Color(0.45f, 1f, 0.5f, 1f);

    [Tooltip("Compact big numbers in Stats (1.2K, 3.4M). Off = full number.")]
    [SerializeField] private bool abbreviateNumbers = false;

    [Header("Restart / Quit")]
    [Tooltip("Show the Restart button (reloads the scene, so all progress is lost).")]
    [SerializeField] private bool showRestart = true;

    [Tooltip("Show the Quit button (stops Play mode in the Editor, closes the game in a build).")]
    [SerializeField] private bool showQuit = true;

    [Tooltip("Menu panel size.")]
    [SerializeField] private Vector2 panelSize = new Vector2(560f, 560f);

    [Tooltip("Title text size.")]
    [SerializeField] private float titleFontSize = 60f;

    [Tooltip("Menu button size.")]
    [SerializeField] private Vector2 menuButtonSize = new Vector2(380f, 90f);

    [Tooltip("Menu button text size.")]
    [SerializeField] private float menuButtonFontSize = 40f;

    [Tooltip("Gap between menu buttons.")]
    [SerializeField] private float menuButtonSpacing = 24f;

    [Tooltip("Colour that dims the screen behind the menu (alpha = darkness).")]
    [SerializeField] private Color dimColor = new Color(0f, 0f, 0f, 0.6f);

    [Tooltip("Panel background colour.")]
    [SerializeField] private Color panelColor = new Color(0.07f, 0.07f, 0.09f, 0.97f);

    [Tooltip("Normal menu button colour.")]
    [SerializeField] private Color menuButtonColor = new Color(0.2f, 0.45f, 0.9f, 1f);

    [Tooltip("Colour of the Quit button.")]
    [SerializeField] private Color quitButtonColor = new Color(0.8f, 0.25f, 0.25f, 1f);

    [Tooltip("Text colour.")]
    [SerializeField] private Color textColor = Color.white;

    [Header("Canvas")]
    [Tooltip("Sorting order of the canvas (above every other panel).")]
    [SerializeField] private int sortingOrder = 500;

    [Tooltip("Reference resolution for the canvas scaler.")]
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);

    /// <summary>True while the game is paused by this menu.</summary>
    public static bool IsPaused { get; private set; }

    /// <summary>True while the menu is open AND it has frozen the game (see the 'Pausing stops the game' setting).</summary>
    public static bool GameStopped { get; private set; }

    private const string PrefPauseStops = "PixelClicker.Setting.PauseStopsGame";
    private bool pauseStopsGame = true;

    private GameObject canvasRoot;
    private GameObject menuRoot;
    private GameObject mainPanel, statsPanel, settingsPanel;
    private TMP_Text totalClicksValue, manualClicksValue, autoClicksValue, timePlayedValue, pixelsSpentValue;
    private Toggle rotationToggle, pulsingToggle, backgroundToggle, pauseStopsToggle;
    private float previousTimeScale = 1f;

    private void Start()
    {
        if (clicker == null)
        {
#if UNITY_2023_1_OR_NEWER
            clicker = FindFirstObjectByType<PixelClicker>();
#else
            clicker = FindObjectOfType<PixelClicker>();
#endif
        }
        if (clicker != null && clicker.UIFont != null) font = clicker.UIFont; // one shared font

        pauseStopsGame = PlayerPrefs.GetInt(PrefPauseStops, 1) != 0;

        if (saveGame == null)
        {
#if UNITY_2023_1_OR_NEWER
            saveGame = FindFirstObjectByType<PixelSaveGame>();
#else
            saveGame = FindObjectOfType<PixelSaveGame>();
#endif
        }
        if (saveGame == null && addSaveGameIfMissing) saveGame = gameObject.AddComponent<PixelSaveGame>();

        if (stats == null)
        {
#if UNITY_2023_1_OR_NEWER
            stats = FindFirstObjectByType<PixelStats>();
#else
            stats = FindObjectOfType<PixelStats>();
#endif
        }
        if (stats == null) stats = gameObject.AddComponent<PixelStats>();

        PixelUIKit.EnsureEventSystem();
        BuildUI();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying || !addSaveGameIfMissing) return;

        // Delayed: components must not be added from inside OnValidate itself.
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || Application.isPlaying) return;
#if UNITY_2023_1_OR_NEWER
            bool has = FindFirstObjectByType<PixelSaveGame>() != null;
#else
            bool has = FindObjectOfType<PixelSaveGame>() != null;
#endif
            if (has) return;
            UnityEditor.Undo.AddComponent<PixelSaveGame>(gameObject);
            UnityEditor.EditorUtility.SetDirty(gameObject);
        };

        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || Application.isPlaying) return;
#if UNITY_2023_1_OR_NEWER
            bool has = FindFirstObjectByType<PixelStats>() != null;
#else
            bool has = FindObjectOfType<PixelStats>() != null;
#endif
            if (has) return;
            UnityEditor.Undo.AddComponent<PixelStats>(gameObject);
            UnityEditor.EditorUtility.SetDirty(gameObject);
        };
    }
#endif

    private void Update()
    {
        if (EscapePressed())
        {
            if (IsPaused)
            {
                // Inside Stats / Settings, Escape steps back; from the main view it resumes.
                if (mainPanel != null && !mainPanel.activeSelf) ShowView(mainPanel);
                else SetPaused(false);
            }
            else if (PixelWindows.CloseTopmost())
            {
                // An open window (placing, upgrades, shop, log, inventory) took the key press.
            }
            else if (pauseKey == PauseKey.Escape)
            {
                SetPaused(true); // only opens when no other window is open
            }
        }
        else if (pauseKey != PauseKey.Escape && PauseKeyPressed())
        {
            SetPaused(!IsPaused);
        }
        if (statsPanel != null && statsPanel.activeSelf) RefreshStats();
    }

    private void OnDestroy()
    {
        if (IsPaused) SetPaused(false);
        if (canvasRoot != null) Destroy(canvasRoot);
    }

    // ------------------------------------------------------------------
    // Pausing
    // ------------------------------------------------------------------

    /// <summary>Pauses or resumes the game. Also callable from other scripts or a UnityEvent.</summary>
    public void SetPaused(bool paused)
    {
        if (paused == IsPaused) return;
        IsPaused = paused;

        ApplyFreeze();

        if (menuRoot != null) menuRoot.SetActive(paused);
        if (paused) ShowView(mainPanel);
    }

    /// <summary>Freezes or unfreezes time to match 'menu open' and the 'Pausing stops the game' setting.</summary>
    private void ApplyFreeze()
    {
        bool freeze = IsPaused && pauseStopsGame;

        if (freeze && !GameStopped)
        {
            previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
        }
        else if (!freeze && GameStopped)
        {
            Time.timeScale = previousTimeScale;
        }

        GameStopped = freeze;
        if (pauseAudio) AudioListener.pause = freeze;
    }

    private static bool EscapePressed()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    private bool PauseKeyPressed()
    {
        if (pauseKey == PauseKey.None) return false;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        Keyboard kb = Keyboard.current;
        if (kb == null) return false;
        switch (pauseKey)
        {
            case PauseKey.Escape: return kb.escapeKey.wasPressedThisFrame;
            case PauseKey.P: return kb.pKey.wasPressedThisFrame;
            case PauseKey.Tab: return kb.tabKey.wasPressedThisFrame;
            case PauseKey.Backspace: return kb.backspaceKey.wasPressedThisFrame;
        }
        return false;
#else
        switch (pauseKey)
        {
            case PauseKey.Escape: return Input.GetKeyDown(KeyCode.Escape);
            case PauseKey.P: return Input.GetKeyDown(KeyCode.P);
            case PauseKey.Tab: return Input.GetKeyDown(KeyCode.Tab);
            case PauseKey.Backspace: return Input.GetKeyDown(KeyCode.Backspace);
        }
        return false;
#endif
    }

    private void Restart()
    {
        if (saveGame != null && restartDeletesSave)
        {
            saveGame.SuppressSaving(); // so autosave / quit can't write the old game back
            saveGame.DeleteSave();
        }
        SetPaused(false);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void Quit()
    {
        SetPaused(false);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ------------------------------------------------------------------
    // UI
    // ------------------------------------------------------------------

    private TMP_Text MakeText(Transform parent, string objectName, string text, float size, FontStyles style)
        => PixelUIKit.CreateText(font, parent, objectName, text, size, TextAlignmentOptions.Center, style, textColor);

    private Button MakeButton(Transform parent, string objectName, string label, Vector2 size, Color color, float labelSize)
        => PixelUIKit.CreateButton(font, parent, objectName, label, size, color, textColor, labelSize);

    private void BuildUI()
    {
        canvasRoot = PixelUIKit.CreateCanvas("PixelPauseMenu Canvas", sortingOrder, referenceResolution, true);

        // On-screen Pause button
        if (showPauseButton)
        {
            Button pause = MakeButton(canvasRoot.transform, "Pause Button", pauseButtonText, buttonSize,
                                      pauseButtonColor, buttonFontSize);
            float ax = buttonAnchor == ButtonAnchor.TopLeft || buttonAnchor == ButtonAnchor.BottomLeft ? 0f
                     : buttonAnchor == ButtonAnchor.TopRight || buttonAnchor == ButtonAnchor.BottomRight ? 1f : 0.5f;
            float ay = buttonAnchor == ButtonAnchor.TopLeft || buttonAnchor == ButtonAnchor.TopCenter
                    || buttonAnchor == ButtonAnchor.TopRight ? 1f : 0f;
            RectTransform pr = pause.GetComponent<RectTransform>();
            pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(ax, ay);
            pr.anchoredPosition = new Vector2(ax < 0.25f ? buttonMargin.x : ax > 0.75f ? -buttonMargin.x : 0f,
                                              ay > 0.5f ? -buttonMargin.y : buttonMargin.y);
            pause.onClick.AddListener(() => SetPaused(true));
        }

        // Menu: full-screen dimmer (also blocks clicks on everything behind it) + centred panel
        menuRoot = new GameObject("Pause Menu", typeof(RectTransform), typeof(Image));
        menuRoot.transform.SetParent(canvasRoot.transform, false);
        Image dim = menuRoot.GetComponent<Image>();
        dim.color = dimColor;
        dim.raycastTarget = true;
        RectTransform dr = menuRoot.GetComponent<RectTransform>();
        dr.anchorMin = Vector2.zero;
        dr.anchorMax = Vector2.one;
        dr.offsetMin = dr.offsetMax = Vector2.zero;

        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        mainPanel = panel;
        panel.transform.SetParent(menuRoot.transform, false);
        panel.GetComponent<Image>().color = panelColor;
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = panelSize;
        panelRect.anchoredPosition = Vector2.zero;

        TMP_Text title = MakeText(panel.transform, "Title", menuTitle, titleFontSize, FontStyles.Bold);
        RectTransform tr = title.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f);
        tr.anchorMax = new Vector2(1f, 1f);
        tr.pivot = new Vector2(0.5f, 1f);
        tr.sizeDelta = new Vector2(0f, titleFontSize * 1.6f);
        tr.anchoredPosition = new Vector2(0f, -30f);

        float y = 30f + titleFontSize * 1.6f + 20f;
        AddMenuButton(panel.transform, resumeText, menuButtonColor, ref y, () => SetPaused(false));
        if (showSaveLoad && saveGame != null)
            AddButtonPair(panel.transform, saveText, () => saveGame.Save(), loadText, () => saveGame.Load(), ref y);

        BuildStatsPanel();
        BuildSettingsPanel();
        if (showStats && showSettings)
            AddButtonPair(panel.transform, statsText, () => ShowView(statsPanel), settingsText, () => ShowView(settingsPanel), ref y);
        else if (showStats) AddMenuButton(panel.transform, statsText, menuButtonColor, ref y, () => ShowView(statsPanel));
        else if (showSettings) AddMenuButton(panel.transform, settingsText, menuButtonColor, ref y, () => ShowView(settingsPanel));
        if (showRestart) AddMenuButton(panel.transform, restartText, menuButtonColor, ref y, Restart);
        if (showQuit) AddMenuButton(panel.transform, quitText, quitButtonColor, ref y, Quit);

        // Grow the panel if the buttons need more room than 'Panel Size' gives.
        float needed = y + 30f;
        if (needed > panelRect.sizeDelta.y) panelRect.sizeDelta = new Vector2(panelRect.sizeDelta.x, needed);

        menuRoot.SetActive(false);
    }

    // ------------------------------------------------------------------
    // Stats and Settings sections
    // ------------------------------------------------------------------

    /// <summary>Shows one of the menu's views (main, stats or settings) and hides the others.</summary>
    private void ShowView(GameObject view)
    {
        if (view == null) return;
        if (mainPanel != null) mainPanel.SetActive(view == mainPanel);
        if (statsPanel != null) statsPanel.SetActive(view == statsPanel);
        if (settingsPanel != null) settingsPanel.SetActive(view == settingsPanel);

        if (view == statsPanel) RefreshStats();
        if (view == settingsPanel && clicker != null)
        {
            rotationToggle.SetIsOnWithoutNotify(clicker.AllowRotation);
            pulsingToggle.SetIsOnWithoutNotify(clicker.AllowPulsing);
            backgroundToggle.SetIsOnWithoutNotify(clicker.RunInBackground);
            pauseStopsToggle.SetIsOnWithoutNotify(pauseStopsGame);
        }
    }

    /// <summary>A centred panel with a title; rows are added below it, then a Back button.</summary>
    private GameObject BuildSectionPanel(string objectName, string title, out float y)
    {
        GameObject panel = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(menuRoot.transform, false);
        panel.GetComponent<Image>().color = panelColor;
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = panelSize;
        rect.anchoredPosition = Vector2.zero;

        TMP_Text t = MakeText(panel.transform, "Title", title, titleFontSize, FontStyles.Bold);
        RectTransform tr = t.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f);
        tr.anchorMax = new Vector2(1f, 1f);
        tr.pivot = new Vector2(0.5f, 1f);
        tr.sizeDelta = new Vector2(0f, titleFontSize * 1.6f);
        tr.anchoredPosition = new Vector2(0f, -30f);

        y = 30f + titleFontSize * 1.6f + 10f;
        return panel;
    }

    /// <summary>Adds the Back button, grows the panel to fit and hides it until it is opened.</summary>
    private void FinishSectionPanel(GameObject panel, float y)
    {
        y += 10f;
        AddMenuButton(panel.transform, backText, menuButtonColor, ref y, () => ShowView(mainPanel));

        RectTransform rect = panel.GetComponent<RectTransform>();
        float needed = y + 30f;
        if (needed > rect.sizeDelta.y) rect.sizeDelta = new Vector2(rect.sizeDelta.x, needed);
        panel.SetActive(false);
    }

    private TMP_Text AddRowLabel(Transform parent, string text, float y, out RectTransform row)
    {
        GameObject go = new GameObject(text + " Row", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        row = go.GetComponent<RectTransform>();
        row.anchorMin = new Vector2(0f, 1f);
        row.anchorMax = new Vector2(1f, 1f);
        row.pivot = new Vector2(0.5f, 1f);
        row.sizeDelta = new Vector2(-80f, rowHeight);
        row.anchoredPosition = new Vector2(0f, -y);

        TMP_Text label = MakeText(go.transform, "Label", text, rowFontSize, FontStyles.Normal);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.enableAutoSizing = true;
        label.fontSizeMax = rowFontSize;
        label.fontSizeMin = 14f;
        RectTransform lr = label.rectTransform;
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = new Vector2(0.62f, 1f);
        lr.offsetMin = lr.offsetMax = Vector2.zero;
        return label;
    }

    private TMP_Text AddStatRow(Transform parent, string label, ref float y)
    {
        AddRowLabel(parent, label, y, out RectTransform row);

        TMP_Text value = MakeText(row, "Value", "", rowFontSize, FontStyles.Bold);
        value.alignment = TextAlignmentOptions.MidlineRight;
        value.color = statValueColor;
        value.enableAutoSizing = true;
        value.fontSizeMax = rowFontSize;
        value.fontSizeMin = 14f;
        RectTransform vr = value.rectTransform;
        vr.anchorMin = new Vector2(0.4f, 0f);
        vr.anchorMax = Vector2.one;
        vr.offsetMin = vr.offsetMax = Vector2.zero;

        y += rowHeight + 6f;
        return value;
    }

    private Toggle AddToggleRow(Transform parent, string label, bool isOn, UnityEngine.Events.UnityAction<bool> onChanged, ref float y)
    {
        AddRowLabel(parent, label, y, out RectTransform row);

        GameObject box = new GameObject("Tick Box", typeof(RectTransform), typeof(Image), typeof(Toggle));
        box.transform.SetParent(row, false);
        Image bg = box.GetComponent<Image>();
        bg.color = tickBoxColor;
        RectTransform br = box.GetComponent<RectTransform>();
        br.anchorMin = br.anchorMax = br.pivot = new Vector2(1f, 0.5f);
        br.sizeDelta = new Vector2(tickBoxSize, tickBoxSize);
        br.anchoredPosition = Vector2.zero;

        GameObject tick = new GameObject("Tick", typeof(RectTransform), typeof(Image));
        tick.transform.SetParent(box.transform, false);
        tick.GetComponent<Image>().color = tickColor;
        tick.GetComponent<Image>().raycastTarget = false;
        RectTransform tkr = tick.GetComponent<RectTransform>();
        tkr.anchorMin = Vector2.zero;
        tkr.anchorMax = Vector2.one;
        tkr.offsetMin = new Vector2(tickBoxSize * 0.2f, tickBoxSize * 0.2f);
        tkr.offsetMax = new Vector2(-tickBoxSize * 0.2f, -tickBoxSize * 0.2f);

        Toggle toggle = box.GetComponent<Toggle>();
        toggle.targetGraphic = bg;
        toggle.graphic = tick.GetComponent<Image>();
        toggle.isOn = isOn;
        toggle.onValueChanged.AddListener(onChanged);

        y += rowHeight + 6f;
        return toggle;
    }

    private void BuildStatsPanel()
    {
        statsPanel = BuildSectionPanel("Stats Panel", statsTitle, out float y);
        totalClicksValue = AddStatRow(statsPanel.transform, totalClicksLabel, ref y);
        manualClicksValue = AddStatRow(statsPanel.transform, manualClicksLabel, ref y);
        autoClicksValue = AddStatRow(statsPanel.transform, autoClicksLabel, ref y);
        timePlayedValue = AddStatRow(statsPanel.transform, timePlayedLabel, ref y);
        pixelsSpentValue = AddStatRow(statsPanel.transform, pixelsSpentLabel, ref y);
        FinishSectionPanel(statsPanel, y);
    }

    private void BuildSettingsPanel()
    {
        settingsPanel = BuildSectionPanel("Settings Panel", settingsTitle, out float y);
        rotationToggle = AddToggleRow(settingsPanel.transform, rotationLabel, clicker == null || clicker.AllowRotation,
                                      on => { if (clicker != null) clicker.AllowRotation = on; }, ref y);
        pulsingToggle = AddToggleRow(settingsPanel.transform, pulsingLabel, clicker == null || clicker.AllowPulsing,
                                     on => { if (clicker != null) clicker.AllowPulsing = on; }, ref y);
        backgroundToggle = AddToggleRow(settingsPanel.transform, runInBackgroundLabel, clicker != null && clicker.RunInBackground,
                                        on => { if (clicker != null) clicker.RunInBackground = on; }, ref y);
        pauseStopsToggle = AddToggleRow(settingsPanel.transform, pauseStopsLabel, pauseStopsGame, on =>
        {
            pauseStopsGame = on;
            PlayerPrefs.SetInt(PrefPauseStops, on ? 1 : 0);
            ApplyFreeze(); // takes effect right away, even though the menu is open
        }, ref y);
        FinishSectionPanel(settingsPanel, y);
    }

    private string FormatCount(double value)
    {
        if (!abbreviateNumbers || value < 1000d) return System.Math.Floor(value).ToString("N0");
        return PixelClicker.FormatNumber(value);
    }

    private void RefreshStats()
    {
        if (stats == null) return;
        totalClicksValue.text = FormatCount(stats.TotalClicks);
        manualClicksValue.text = FormatCount(stats.ManualClicks);
        autoClicksValue.text = FormatCount(stats.AutoClicks);
        timePlayedValue.text = PixelStats.FormatTime(stats.PlaySeconds);
        pixelsSpentValue.text = FormatCount(stats.PixelsSpent);
    }

    /// <summary>Two half-width buttons side by side on one row (keeps the menu short).</summary>
    private void AddButtonPair(Transform parent, string labelA, UnityEngine.Events.UnityAction a,
                               string labelB, UnityEngine.Events.UnityAction b, ref float y)
    {
        float gap = 16f;
        Vector2 half = new Vector2((menuButtonSize.x - gap) * 0.5f, menuButtonSize.y);
        string[] labels = { labelA, labelB };
        UnityEngine.Events.UnityAction[] actions = { a, b };
        for (int i = 0; i < 2; i++)
        {
            Button button = MakeButton(parent, labels[i] + " Button", labels[i], half, menuButtonColor, menuButtonFontSize);
            RectTransform rt = button.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2((i == 0 ? -1f : 1f) * (half.x * 0.5f + gap * 0.5f), -y);
            button.onClick.AddListener(actions[i]);
        }
        y += menuButtonSize.y + menuButtonSpacing;
    }

    private void AddMenuButton(Transform parent, string label, Color color, ref float y, UnityEngine.Events.UnityAction onClick)
    {
        Button button = MakeButton(parent, label + " Button", label, menuButtonSize, color, menuButtonFontSize);
        RectTransform rt = button.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -y);
        button.onClick.AddListener(onClick);
        y += menuButtonSize.y + menuButtonSpacing;
    }
}
