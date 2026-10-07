using System.Collections.Generic;
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
    [Tooltip("Show a Pause button on screen. Off by default: Escape (or the Pause Key) pauses instead, and the bottom-right corner is used by the Crafting button.")]
    [SerializeField] private bool showPauseButtonOnScreen = false;

    [Tooltip("Where the Pause button sits.")]
    [SerializeField] private ButtonAnchor pauseButtonAnchor = ButtonAnchor.BottomRight;

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

    [Header("Restart Confirmation")]
    [Tooltip("Title of the confirmation screen.")]
    [SerializeField] private string restartConfirmTitle = "Restart?";

    [TextArea(2, 4)]
    [Tooltip("Warning shown on the confirmation screen.")]
    [SerializeField] private string restartConfirmMessage = "This will delete EVERYTHING: your pixels, shop purchases, achievements and stats. This cannot be undone.";

    [Tooltip("Text on the hold-to-confirm button.")]
    [SerializeField] private string restartHoldText = "Hold to delete everything";

    [Min(0.2f)]
    [Tooltip("Seconds the player must keep the mouse button held on the confirm button.")]
    [SerializeField] private float restartHoldSeconds = 2f;

    [Tooltip("Colour of the confirm button.")]
    [SerializeField] private Color restartHoldColor = new Color(0.8f, 0.25f, 0.25f, 1f);

    [Tooltip("Colour of the fill that grows across the confirm button while it is held.")]
    [SerializeField] private Color restartFillColor = new Color(1f, 0.85f, 0.3f, 0.85f);

    [Tooltip("Colour of the warning text.")]
    [SerializeField] private Color restartWarningColor = new Color(1f, 0.8f, 0.7f, 1f);

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

    [Tooltip("Label of the tick box that switches between abbreviated and full (1,200) numbers.")]
    [SerializeField] private string abbreviateLabel = "Abbreviate numbers";

    [Tooltip("Label of the tick box that hides shop items you have already bought.")]
    [SerializeField] private string hidePurchasedLabel = "Hide purchased shop items";

    [Tooltip("Label of the tick box that decides whether the pause menu freezes the game.")]
    [SerializeField] private string pauseStopsLabel = "Pausing stops the game";

    [Tooltip("Label of the tick box that keeps the game running while you are alt-tabbed.")]
    [SerializeField] private string runInBackgroundLabel = "Run when alt-tabbed";

    [Tooltip("Label of the master volume slider.")]
    [SerializeField] private string masterVolumeLabel = "Master volume";

    [Tooltip("Label of the effects volume slider.")]
    [SerializeField] private string effectsVolumeLabel = "Effects volume";

    [Tooltip("Label of the music volume slider.")]
    [SerializeField] private string musicVolumeLabel = "Music volume";

    [Tooltip("Label of the mute tick box.")]
    [SerializeField] private string muteLabel = "Mute all sound";

    [Tooltip("Colour of the volume slider track.")]
    [SerializeField] private Color sliderTrackColor = new Color(0.25f, 0.25f, 0.3f, 1f);

    [Tooltip("Colour of the filled part of a volume slider.")]
    [SerializeField] private Color sliderFillColor = new Color(0.35f, 0.55f, 0.95f, 1f);

    [Tooltip("Colour of a volume slider's handle.")]
    [SerializeField] private Color sliderHandleColor = Color.white;

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

    [Tooltip("Label of the ghosts clicked stat.")]
    [SerializeField] private string ghostsClickedLabel = "Ghosts clicked";

    [Tooltip("Label of the meteors clicked stat.")]
    [SerializeField] private string meteorsClickedLabel = "Meteors clicked";

    [Tooltip("Label of the meteors spawned stat.")]
    [SerializeField] private string meteorsSpawnedLabel = "Meteors spawned";

    [Tooltip("Label of the black holes spawned stat.")]
    [SerializeField] private string blackHolesSpawnedLabel = "Black holes spawned";

    [Tooltip("Label of the fans used stat.")]
    [SerializeField] private string fansUsedLabel = "Fans used";

    [Tooltip("Label of the vacuum devices used stat.")]
    [SerializeField] private string vacuumsUsedLabel = "Vacuum devices used";

    [Tooltip("Label of the highest combo stat.")]
    [SerializeField] private string highestComboLabel = "Highest combo";

    [Tooltip("Text of the collapsible potions section's button. {0} = total potions used, {1} = + or - .")]
    [SerializeField] private string potionsUsedFormat = "Potions used ({0})  [{1}]";

    [Tooltip("Line in the potions box. {0} = potion name, {1} = how many were used.")]
    [SerializeField] private string potionLineFormat = "{0}   x{1}";

    [Tooltip("Shown in the potions box before any potion was used.")]
    [SerializeField] private string noPotionsText = "None yet";

    [Min(100f)]
    [Tooltip("Height of the scrolling stats list (canvas units). More stats than fit scroll.")]
    [SerializeField] private float statsViewHeight = 480f;

    [Min(60f)]
    [Tooltip("Height of the potions-used box (canvas units). A longer list scrolls.")]
    [SerializeField] private float potionsBoxHeight = 170f;

    [Tooltip("Open the potions-used section when the Stats screen is shown.")]
    [SerializeField] private bool potionsOpenByDefault = false;

    [Tooltip("Scroll bar colour in the Stats screen.")]
    [SerializeField] private Color scrollbarColor = new Color(1f, 1f, 1f, 0.35f);

    [Tooltip("Background of the potions-used box.")]
    [SerializeField] private Color potionsBoxColor = new Color(0f, 0f, 0f, 0.35f);

    [Header("Changelog")]
    [Tooltip("Show the Changelog button in the pause menu.")]
    [SerializeField] private bool showChangelog = true;

    [Tooltip("Text of the Changelog button and the title of its screen.")]
    [SerializeField] private string changelogText = "Changelog";

    [Tooltip("Name of the text file in a Resources folder that holds the changelog (Assets/Resources/Changelog.txt), one change per line.")]
    [SerializeField] private string changelogResource = "Changelog";

    [Tooltip("Shown when the changelog is empty.")]
    [SerializeField] private string emptyChangelogText = "No changes since the last build.";

    [Tooltip("Newest change first.")]
    [SerializeField] private bool newestFirst = true;

    [Min(300f)]
    [Tooltip("Width of the changelog screen (canvas units).")]
    [SerializeField] private float changelogWidth = 980f;

    [Min(100f)]
    [Tooltip("Height of the scrolling changelog text (canvas units).")]
    [SerializeField] private float changelogViewHeight = 560f;

    [Tooltip("Size of the changelog text.")]
    [SerializeField] private float changelogFontSize = 28f;

    [Header("Report Folder")]
    [Tooltip("Show the Report Folder button (opens the folder with the crash / error reports, for sending to the developer).")]
    [SerializeField] private bool showReportFolder = true;

    [Tooltip("Text of the Report Folder button.")]
    [SerializeField] private string reportFolderText = "Report Folder";

    [Header("How to Play and Controls")]
    [Tooltip("Show the How to Play button in the pause menu.")]
    [SerializeField] private bool showHowToPlay = true;

    [Tooltip("Text of the How to Play button and the title of its screen.")]
    [SerializeField] private string howToPlayText = "How to Play";

    [Tooltip("Name of the text file in a Resources folder that holds the How to Play text (Assets/Resources/HowToPlay.txt). Edit that file to change what the screen says.")]
    [SerializeField] private string howToPlayResource = "HowToPlay";

    [Tooltip("Shown when the How to Play file is missing or empty.")]
    [SerializeField] private string emptyHowToPlayText = "Nothing to show yet. Add text to Assets/Resources/HowToPlay.txt.";

    [Tooltip("Show the Controls button in the pause menu.")]
    [SerializeField] private bool showControls = true;

    [Tooltip("Text of the Controls button and the title of its screen.")]
    [SerializeField] private string controlsText = "Controls";

    [Tooltip("Name of the text file in a Resources folder that holds the controls list (Assets/Resources/Controls.txt). Edit that file to change what the screen says.")]
    [SerializeField] private string controlsResource = "Controls";

    [Tooltip("Shown when the Controls file is missing or empty.")]
    [SerializeField] private string emptyControlsText = "Nothing to show yet. Add text to Assets/Resources/Controls.txt.";

    [Min(300f)]
    [Tooltip("Width of the How to Play / Controls screens (canvas units).")]
    [SerializeField] private float guideWidth = 1100f;

    [Min(100f)]
    [Tooltip("Height of the scrolling text on those screens (canvas units).")]
    [SerializeField] private float guideViewHeight = 600f;

    [Tooltip("Size of the text on those screens.")]
    [SerializeField] private float guideFontSize = 28f;

    [Tooltip("Colour of the # headings in those files.")]
    [SerializeField] private Color guideHeadingColor = new Color(1f, 0.85f, 0.4f, 1f);

    [Range(100f, 200f)]
    [Tooltip("Size of a # heading, in percent of the normal text.")]
    [SerializeField] private float guideHeadingPercent = 130f;

    [Range(10f, 70f)]
    [Tooltip("Where the second column starts on a 'Key | Action' line, as a percentage of the width.")]
    [SerializeField] private float guideColumnPercent = 40f;

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

    [Header("Restart / Quit")]
    [Tooltip("Show the Restart button (reloads the scene, so all progress is lost).")]
    [SerializeField] private bool showRestart = true;

    [Tooltip("Show the Quit button (stops Play mode in the Editor, closes the game in a build).")]
    [SerializeField] private bool showQuit = true;

    [Tooltip("Menu panel size. The main menu uses its own width (Main Panel Width) and fits its height to its buttons.")]
    [SerializeField] private Vector2 panelSize = new Vector2(560f, 560f);

    [Header("Layout")]
    [Min(300f)]
    [Tooltip("Width of the main pause menu (canvas units). Wider than before so the buttons sit in columns and the menu is shorter.")]
    [SerializeField] private float mainPanelWidth = 1000f;

    [Range(1, 4)]
    [Tooltip("How many columns of buttons the main menu has.")]
    [SerializeField] private int mainColumns = 2;

    [Min(0f)]
    [Tooltip("Gap between buttons in the same row.")]
    [SerializeField] private float columnGap = 20f;

    [Tooltip("Keep the menu (and the Changelog / How to Play / Controls screens) between the black bars at the top and bottom of the screen: if it would be taller, the buttons or scrolling text shrink to fit.")]
    [SerializeField] private bool fitBetweenBars = true;

    [Min(0f)]
    [Tooltip("Extra empty space kept between the menu and each black bar.")]
    [SerializeField] private float barMargin = 20f;

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
    private GameObject mainPanel, statsPanel, settingsPanel, changelogPanel, restartPanel;
    private Slider masterSlider, effectsSlider, musicSlider;
    private Toggle muteToggle;
    private PixelHoldButton restartHold;
    private ScrollRect changelogScroll;
    private GameObject changelogBar;
    private TMP_Text changelogLabel;

    /// <summary>One of the scrolling text screens fed from a Resources text file (How to Play, Controls).</summary>
    private class GuideScreen
    {
        public GameObject panel;
        public ScrollRect scroll;
        public GameObject bar;
        public TMP_Text label;
        public string resource, empty;
        public float viewHeight;
    }

    private GuideScreen howToPlayScreen, controlsScreen;
    private float changelogFitHeight;

    private struct MenuEntry
    {
        public string label;
        public Color color;
        public UnityEngine.Events.UnityAction action;
    }
    private TMP_Text totalClicksValue, manualClicksValue, autoClicksValue, timePlayedValue, pixelsSpentValue;
    private TMP_Text ghostsValue, meteorsClickedValue, meteorsSpawnedValue, blackHolesValue, fansValue, vacuumsValue, comboValue;
    private ScrollRect statsScroll, potionsScroll;
    private GameObject statsBar, potionsBar, potionsBox;
    private TMP_Text potionsHeaderLabel, potionsText;
    private RectTransform statsPanelRect, potionsHeaderRect, potionsBoxRect, statsBackRect;
    private float statsContentHeight, statsListTop;
    private bool potionsOpen;
    private string shownPotionsText;
    private Toggle rotationToggle, pulsingToggle, abbreviateToggle, hidePurchasedToggle, backgroundToggle, pauseStopsToggle;
    private float previousTimeScale = 1f;

    private void Start()
    {
        if (clicker == null)
        {
            clicker = PixelFind.First<PixelClicker>();
        }
        if (clicker != null && clicker.UIFont != null) font = clicker.UIFont; // one shared font

        pauseStopsGame = PlayerPrefs.GetInt(PrefPauseStops, 1) != 0;

        if (saveGame == null)
        {
            saveGame = PixelFind.First<PixelSaveGame>();
        }
        if (saveGame == null && addSaveGameIfMissing) saveGame = gameObject.AddComponent<PixelSaveGame>();

        if (stats == null)
        {
            stats = PixelFind.First<PixelStats>();
        }
        if (stats == null) stats = gameObject.AddComponent<PixelStats>();
        if (PixelFind.First<PixelAudio>() == null) gameObject.AddComponent<PixelAudio>(); // the sound system (and its volume settings)
        PixelHud.Ensure(gameObject); // the black bars and docked buttons
        if (PixelFind.First<PixelToggles>() == null) gameObject.AddComponent<PixelToggles>(); // the Toggles window

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
            bool has = PixelFind.First<PixelSaveGame>() != null;
            if (has) return;
            UnityEditor.Undo.AddComponent<PixelSaveGame>(gameObject);
            UnityEditor.EditorUtility.SetDirty(gameObject);
        };

        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || Application.isPlaying) return;
            bool has = PixelFind.First<PixelStats>() != null;
            if (has) return;
            UnityEditor.Undo.AddComponent<PixelStats>(gameObject);
            UnityEditor.EditorUtility.SetDirty(gameObject);
        };

        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || Application.isPlaying) return;
            if (PixelFind.First<PixelAudio>() != null) return;
            UnityEditor.Undo.AddComponent<PixelAudio>(gameObject);
            UnityEditor.EditorUtility.SetDirty(gameObject);
        };

        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || Application.isPlaying) return;
            if (PixelFind.First<PixelHud>() != null) return;
            UnityEditor.Undo.AddComponent<PixelHud>(gameObject);
            UnityEditor.EditorUtility.SetDirty(gameObject);
        };

        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || Application.isPlaying) return;
            if (PixelFind.First<PixelToggles>() != null) return;
            UnityEditor.Undo.AddComponent<PixelToggles>(gameObject);
            UnityEditor.EditorUtility.SetDirty(gameObject);
        };
    }
#endif

    private void Update()
    {
        if (EscapePressed())
        {
            if (IsPaused && PixelDevTools.ClosePanel())
            {
                // The dev tools panel (opened from this menu) took the key press.
            }
            else if (IsPaused)
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
        PixelAchievements.ResetOnNextStart = true; // achievements start from nothing in the new game too
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
        if (showPauseButtonOnScreen)
        {
            Button pause = MakeButton(canvasRoot.transform, "Pause Button", pauseButtonText, buttonSize,
                                      pauseButtonColor, buttonFontSize);
            float ax = pauseButtonAnchor == ButtonAnchor.TopLeft || pauseButtonAnchor == ButtonAnchor.BottomLeft ? 0f
                     : pauseButtonAnchor == ButtonAnchor.TopRight || pauseButtonAnchor == ButtonAnchor.BottomRight ? 1f : 0.5f;
            float ay = pauseButtonAnchor == ButtonAnchor.TopLeft || pauseButtonAnchor == ButtonAnchor.TopCenter
                    || pauseButtonAnchor == ButtonAnchor.TopRight ? 1f : 0f;
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
        List<MenuEntry> entries = new List<MenuEntry>();
        entries.Add(new MenuEntry { label = resumeText, color = menuButtonColor, action = () => SetPaused(false) });
        if (showSaveLoad && saveGame != null)
        {
            entries.Add(new MenuEntry { label = saveText, color = menuButtonColor, action = () => saveGame.Save() });
            entries.Add(new MenuEntry { label = loadText, color = menuButtonColor, action = () => saveGame.Load() });
        }

        BuildStatsPanel();
        BuildSettingsPanel();
        if (showStats) entries.Add(new MenuEntry { label = statsText, color = menuButtonColor, action = () => ShowView(statsPanel) });
        if (showSettings) entries.Add(new MenuEntry { label = settingsText, color = menuButtonColor, action = () => ShowView(settingsPanel) });
        if (PixelDevTools.Available) entries.Add(new MenuEntry { label = PixelDevTools.ButtonText, color = menuButtonColor, action = PixelDevTools.OpenPanel });
        BuildChangelogPanel();
        BuildRestartPanel();
        if (showHowToPlay) howToPlayScreen = BuildGuideScreen("How To Play Panel", howToPlayText, howToPlayResource, emptyHowToPlayText);
        if (showControls) controlsScreen = BuildGuideScreen("Controls Panel", controlsText, controlsResource, emptyControlsText);
        if (howToPlayScreen != null) entries.Add(new MenuEntry { label = howToPlayText, color = menuButtonColor, action = () => ShowView(howToPlayScreen.panel) });
        if (controlsScreen != null) entries.Add(new MenuEntry { label = controlsText, color = menuButtonColor, action = () => ShowView(controlsScreen.panel) });
        if (showChangelog) entries.Add(new MenuEntry { label = changelogText, color = menuButtonColor, action = () => ShowView(changelogPanel) });
        if (showReportFolder && PixelCrashLog.Available)
            entries.Add(new MenuEntry { label = reportFolderText, color = menuButtonColor, action = PixelCrashLog.OpenFolder });
        if (showRestart) entries.Add(new MenuEntry { label = restartText, color = menuButtonColor, action = () => ShowView(restartPanel) });
        if (showQuit) entries.Add(new MenuEntry { label = quitText, color = quitButtonColor, action = Quit });

        LayoutMainButtons(panel.transform, entries, y, panelRect);

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
        if (changelogPanel != null) changelogPanel.SetActive(view == changelogPanel);
        if (howToPlayScreen != null) howToPlayScreen.panel.SetActive(view == howToPlayScreen.panel);
        if (controlsScreen != null) controlsScreen.panel.SetActive(view == controlsScreen.panel);
        if (howToPlayScreen != null && view == howToPlayScreen.panel) RefreshGuideScreen(howToPlayScreen);
        if (controlsScreen != null && view == controlsScreen.panel) RefreshGuideScreen(controlsScreen);
        if (restartPanel != null) restartPanel.SetActive(view == restartPanel);
        if (view == restartPanel && restartHold != null) restartHold.ResetProgress();
        if (view == changelogPanel) RefreshChangelog();

        if (view == statsPanel) RefreshStats();
        if (view == settingsPanel && clicker != null)
        {
            rotationToggle.SetIsOnWithoutNotify(clicker.AllowRotation);
            pulsingToggle.SetIsOnWithoutNotify(clicker.AllowPulsing);
            abbreviateToggle.SetIsOnWithoutNotify(PixelClicker.AbbreviateNumbers);
            hidePurchasedToggle.SetIsOnWithoutNotify(PixelShop.HidePurchased);
            backgroundToggle.SetIsOnWithoutNotify(clicker.RunInBackground);
            pauseStopsToggle.SetIsOnWithoutNotify(pauseStopsGame);
        }
        if (view == settingsPanel && PixelAudio.Instance != null && masterSlider != null)
        {
            masterSlider.SetValueWithoutNotify(PixelAudio.Instance.MasterVolume);
            effectsSlider.SetValueWithoutNotify(PixelAudio.Instance.EffectsVolume);
            musicSlider.SetValueWithoutNotify(PixelAudio.Instance.MusicVolume);
            muteToggle.SetIsOnWithoutNotify(PixelAudio.Instance.Muted);
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
        toggle.onValueChanged.AddListener(_ => PixelAudio.Play("ui_click"));

        y += rowHeight + 6f;
        return toggle;
    }

    /// <summary>A label with a 0..1 slider on its right.</summary>
    private Slider AddSliderRow(Transform parent, string label, float value, UnityEngine.Events.UnityAction<float> onChanged, ref float y)
    {
        AddRowLabel(parent, label, y, out RectTransform row);

        Slider slider = PixelUIKit.CreateSlider(row, "Slider", sliderTrackColor, sliderFillColor, sliderHandleColor);
        RectTransform sr = slider.GetComponent<RectTransform>();
        sr.anchorMin = new Vector2(0.62f, 0f);
        sr.anchorMax = Vector2.one;
        sr.offsetMin = sr.offsetMax = Vector2.zero;
        slider.SetValueWithoutNotify(value);
        slider.onValueChanged.AddListener(onChanged);

        y += rowHeight + 6f;
        return slider;
    }

    private void BuildStatsPanel()
    {
        statsPanel = BuildSectionPanel("Stats Panel", statsTitle, out float y);
        statsPanelRect = statsPanel.GetComponent<RectTransform>();
        statsListTop = y;
        potionsOpen = potionsOpenByDefault;

        // The stats scroll when there are more than fit.
        statsScroll = PixelUIKit.CreateScrollView(statsPanel.transform, "Stats List", scrollbarColor, 12f, rowHeight,
                                                  out RectTransform content, out statsBar);
        RectTransform vr = statsScroll.GetComponent<RectTransform>();
        vr.anchorMin = new Vector2(0f, 1f);
        vr.anchorMax = new Vector2(1f, 1f);
        vr.pivot = new Vector2(0.5f, 1f);
        vr.sizeDelta = new Vector2(0f, statsViewHeight);
        vr.anchoredPosition = new Vector2(0f, -y);

        float cy = 0f;
        totalClicksValue = AddStatRow(content, totalClicksLabel, ref cy);
        manualClicksValue = AddStatRow(content, manualClicksLabel, ref cy);
        autoClicksValue = AddStatRow(content, autoClicksLabel, ref cy);
        timePlayedValue = AddStatRow(content, timePlayedLabel, ref cy);
        pixelsSpentValue = AddStatRow(content, pixelsSpentLabel, ref cy);
        ghostsValue = AddStatRow(content, ghostsClickedLabel, ref cy);
        meteorsClickedValue = AddStatRow(content, meteorsClickedLabel, ref cy);
        meteorsSpawnedValue = AddStatRow(content, meteorsSpawnedLabel, ref cy);
        blackHolesValue = AddStatRow(content, blackHolesSpawnedLabel, ref cy);
        fansValue = AddStatRow(content, fansUsedLabel, ref cy);
        vacuumsValue = AddStatRow(content, vacuumsUsedLabel, ref cy);
        comboValue = AddStatRow(content, highestComboLabel, ref cy);
        statsContentHeight = cy;

        // Collapsible "potions used" section: a button, then a box with a scroll bar.
        Vector2 wide = new Vector2(panelSize.x - 80f, rowHeight);
        Button header = MakeButton(statsPanel.transform, "Potions Header", "", wide, tickBoxColor, rowFontSize * 0.9f);
        potionsHeaderLabel = header.GetComponentInChildren<TMP_Text>();
        potionsHeaderRect = header.GetComponent<RectTransform>();
        potionsHeaderRect.anchorMin = potionsHeaderRect.anchorMax = potionsHeaderRect.pivot = new Vector2(0.5f, 1f);
        header.onClick.AddListener(() => { potionsOpen = !potionsOpen; RefreshStats(); });

        potionsBox = new GameObject("Potions Box", typeof(RectTransform), typeof(Image));
        potionsBox.transform.SetParent(statsPanel.transform, false);
        potionsBox.GetComponent<Image>().color = potionsBoxColor;
        potionsBoxRect = potionsBox.GetComponent<RectTransform>();
        potionsBoxRect.anchorMin = potionsBoxRect.anchorMax = potionsBoxRect.pivot = new Vector2(0.5f, 1f);
        potionsBoxRect.sizeDelta = new Vector2(wide.x, potionsBoxHeight);

        potionsScroll = PixelUIKit.CreateScrollView(potionsBox.transform, "Potions List", scrollbarColor, 12f, rowFontSize * 1.3f,
                                                    out RectTransform potionsContent, out potionsBar);
        RectTransform pr = potionsScroll.GetComponent<RectTransform>();
        PixelUIKit.Stretch(pr);
        pr.offsetMin = new Vector2(8f, 8f);
        pr.offsetMax = new Vector2(-8f, -8f);
        potionsText = MakeText(potionsContent, "Potions Text", "", rowFontSize * 0.85f, FontStyles.Normal);
        potionsText.alignment = TextAlignmentOptions.TopLeft;
        potionsText.color = statValueColor;
        RectTransform tr = potionsText.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f);
        tr.anchorMax = new Vector2(1f, 1f);
        tr.pivot = new Vector2(0.5f, 1f);
        tr.offsetMin = new Vector2(8f, -rowHeight);
        tr.offsetMax = new Vector2(-24f, 0f);

        // Back button (moves with the section)
        Button back = MakeButton(statsPanel.transform, "Back Button", backText, menuButtonSize, menuButtonColor, menuButtonFontSize);
        statsBackRect = back.GetComponent<RectTransform>();
        statsBackRect.anchorMin = statsBackRect.anchorMax = statsBackRect.pivot = new Vector2(0.5f, 1f);
        back.onClick.AddListener(() => ShowView(mainPanel));

        LayoutStats();
        statsPanel.SetActive(false);
    }

    /// <summary>Places the potions section and the Back button, and sizes the Stats panel (it grows when the potions box is open).</summary>
    private void LayoutStats()
    {
        float y = statsListTop + statsViewHeight + 8f;
        potionsHeaderRect.anchoredPosition = new Vector2(0f, -y);
        y += rowHeight + 6f;

        potionsBox.SetActive(potionsOpen);
        if (potionsOpen)
        {
            potionsBoxRect.anchoredPosition = new Vector2(0f, -y);
            y += potionsBoxHeight + 10f;
        }
        else y += 4f;

        statsBackRect.anchoredPosition = new Vector2(0f, -y);
        y += menuButtonSize.y + 30f;
        statsPanelRect.sizeDelta = new Vector2(panelSize.x, Mathf.Max(panelSize.y, y));
    }

    /// <summary>"This deletes everything" screen: the player has to HOLD the red button to confirm.</summary>
    private void BuildRestartPanel()
    {
        restartPanel = BuildSectionPanel("Restart Panel", restartConfirmTitle, out float y);

        TMP_Text warning = MakeText(restartPanel.transform, "Warning", restartConfirmMessage, rowFontSize * 0.9f, FontStyles.Normal);
        warning.color = restartWarningColor;
        warning.alignment = TextAlignmentOptions.Top;
        float width = panelSize.x - 80f;
        float height = Mathf.Ceil(warning.GetPreferredValues(restartConfirmMessage, width, 0f).y) + 10f;
        RectTransform wr = warning.rectTransform;
        wr.anchorMin = new Vector2(0.5f, 1f);
        wr.anchorMax = new Vector2(0.5f, 1f);
        wr.pivot = new Vector2(0.5f, 1f);
        wr.sizeDelta = new Vector2(width, height);
        wr.anchoredPosition = new Vector2(0f, -y);
        y += height + 20f;

        // The hold-to-confirm button: a fill grows across it while the mouse button stays down.
        Button hold = MakeButton(restartPanel.transform, "Hold To Confirm", "", menuButtonSize, restartHoldColor, menuButtonFontSize);
        RectTransform hr = hold.GetComponent<RectTransform>();
        hr.anchorMin = hr.anchorMax = hr.pivot = new Vector2(0.5f, 1f);
        hr.anchoredPosition = new Vector2(0f, -y);
        TMP_Text holdLabel = hold.GetComponentInChildren<TMP_Text>();
        holdLabel.text = restartHoldText;
        holdLabel.enableAutoSizing = true;
        holdLabel.fontSizeMax = menuButtonFontSize * 0.8f;
        holdLabel.fontSizeMin = 14f;
        holdLabel.transform.SetAsLastSibling();

        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(hold.transform, false);
        fill.transform.SetSiblingIndex(0); // behind the label
        Image fillImage = fill.GetComponent<Image>();
        fillImage.color = restartFillColor;
        fillImage.raycastTarget = false;
        RectTransform fr = fill.GetComponent<RectTransform>();
        PixelUIKit.Stretch(fr);

        restartHold = hold.gameObject.AddComponent<PixelHoldButton>();
        restartHold.Setup(fr, restartHoldSeconds, Restart);
        y += menuButtonSize.y + menuButtonSpacing;

        FinishSectionPanel(restartPanel, y);
    }

    private void BuildChangelogPanel()
    {
        changelogPanel = BuildSectionPanel("Changelog Panel", changelogText, out float y);
        changelogFitHeight = FitViewHeight(changelogViewHeight, y);
        RectTransform pr = changelogPanel.GetComponent<RectTransform>();
        pr.sizeDelta = new Vector2(changelogWidth, pr.sizeDelta.y);

        changelogScroll = PixelUIKit.CreateScrollView(changelogPanel.transform, "Changelog List", scrollbarColor, 12f,
                                                      changelogFontSize * 1.5f, out RectTransform content, out changelogBar);
        RectTransform vr = changelogScroll.GetComponent<RectTransform>();
        vr.anchorMin = new Vector2(0f, 1f);
        vr.anchorMax = new Vector2(1f, 1f);
        vr.pivot = new Vector2(0.5f, 1f);
        vr.sizeDelta = new Vector2(-60f, changelogFitHeight);
        vr.anchoredPosition = new Vector2(0f, -y);

        changelogLabel = MakeText(content, "Changelog Text", "", changelogFontSize, FontStyles.Normal);
        changelogLabel.alignment = TextAlignmentOptions.TopLeft;
        changelogLabel.richText = false; // the text is shown exactly as written
        RectTransform lr = changelogLabel.rectTransform;
        lr.anchorMin = new Vector2(0f, 1f);
        lr.anchorMax = new Vector2(1f, 1f);
        lr.pivot = new Vector2(0.5f, 1f);
        lr.offsetMin = new Vector2(10f, -changelogFitHeight);
        lr.offsetMax = new Vector2(-26f, 0f);

        FinishSectionPanel(changelogPanel, y + changelogFitHeight);
    }

    private GuideScreen BuildGuideScreen(string objectName, string title, string resource, string emptyText)
    {
        GuideScreen s = new GuideScreen { resource = resource, empty = emptyText };
        s.panel = BuildSectionPanel(objectName, title, out float y);
        RectTransform pr = s.panel.GetComponent<RectTransform>();
        s.viewHeight = FitViewHeight(guideViewHeight, y);
        pr.sizeDelta = new Vector2(guideWidth, pr.sizeDelta.y);

        s.scroll = PixelUIKit.CreateScrollView(s.panel.transform, title + " List", scrollbarColor, 12f, guideFontSize * 1.5f,
                                               out RectTransform content, out s.bar);
        RectTransform vr = s.scroll.GetComponent<RectTransform>();
        vr.anchorMin = new Vector2(0f, 1f);
        vr.anchorMax = new Vector2(1f, 1f);
        vr.pivot = new Vector2(0.5f, 1f);
        vr.sizeDelta = new Vector2(-60f, s.viewHeight);
        vr.anchoredPosition = new Vector2(0f, -y);

        s.label = MakeText(content, title + " Text", "", guideFontSize, FontStyles.Normal);
        s.label.alignment = TextAlignmentOptions.TopLeft;
        s.label.richText = true;
        RectTransform lr = s.label.rectTransform;
        lr.anchorMin = new Vector2(0f, 1f);
        lr.anchorMax = new Vector2(1f, 1f);
        lr.pivot = new Vector2(0.5f, 1f);
        lr.offsetMin = new Vector2(10f, -s.viewHeight);
        lr.offsetMax = new Vector2(-26f, 0f);

        FinishSectionPanel(s.panel, y + s.viewHeight);
        return s;
    }

    /// <summary>Reloads the screen's text file and shows it.</summary>
    private void RefreshGuideScreen(GuideScreen s)
    {
        TextAsset asset = Resources.Load<TextAsset>(s.resource);
        string text = FormatGuideText(asset != null ? asset.text : "");
        if (text.Trim().Length == 0) text = s.empty;
        s.label.text = text;

        float width = s.label.rectTransform.rect.width > 1f ? s.label.rectTransform.rect.width : guideWidth - 100f;
        float height = Mathf.Ceil(s.label.GetPreferredValues(text, width, 0f).y) + 10f;
        s.label.rectTransform.offsetMin = new Vector2(10f, -height);
        s.scroll.content.anchoredPosition = Vector2.zero;
        PixelUIKit.UpdateScrollView(s.scroll, s.bar, height, s.viewHeight);
    }

    /// <summary>
    /// Turns the text file into what is shown: lines starting with // are skipped, a line starting with # is a coloured
    /// heading, and "Key | Action" becomes two columns. Everything else is shown as written.
    /// </summary>
    private string FormatGuideText(string raw)
    {
        string headingHex = ColorUtility.ToHtmlStringRGB(guideHeadingColor);
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        bool first = true;

        foreach (string rawLine in raw.Replace("\r", "").Split('\n'))
        {
            string line = rawLine.TrimEnd();
            if (line.TrimStart().StartsWith("//")) continue;

            string trimmed = line.TrimStart();
            string shown;
            if (trimmed.StartsWith("#"))
            {
                shown = "<size=" + guideHeadingPercent.ToString("0") + "%><b><color=#" + headingHex + ">" +
                        trimmed.TrimStart('#').Trim() + "</color></b></size>";
                if (!first) sb.Append('\n'); // an extra blank line above each heading
            }
            else if (line.Contains(" | "))
            {
                int bar = line.IndexOf(" | ", System.StringComparison.Ordinal);
                shown = "<b>" + line.Substring(0, bar).Trim() + "</b><pos=" + guideColumnPercent.ToString("0") + "%>" +
                        line.Substring(bar + 3).Trim();
            }
            else shown = line;

            if (!first) sb.Append('\n');
            sb.Append(shown);
            first = false;
        }
        return sb.ToString();
    }

    /// <summary>Reads the changelog file (one change per line) and shows it, newest first.</summary>
    private void RefreshChangelog()
    {
        TextAsset asset = Resources.Load<TextAsset>(changelogResource);
        string[] lines = asset != null ? asset.text.Split('\n') : new string[0];

        System.Collections.Generic.List<string> entries = new System.Collections.Generic.List<string>();
        foreach (string raw in lines)
        {
            string line = raw.Trim();
            if (line.Length > 0) entries.Add(line);
        }
        if (newestFirst) entries.Reverse();

        // A blank line between entries keeps wrapped lines readable.
        string text = entries.Count > 0 ? string.Join("\n\n", entries) : emptyChangelogText;
        changelogLabel.text = text;

        float width = changelogLabel.rectTransform.rect.width > 1f ? changelogLabel.rectTransform.rect.width : changelogWidth - 100f;
        float height = Mathf.Ceil(changelogLabel.GetPreferredValues(text, width, 0f).y) + 10f;
        changelogLabel.rectTransform.offsetMin = new Vector2(10f, -height);
        PixelUIKit.UpdateScrollView(changelogScroll, changelogBar, height, changelogFitHeight);
    }

    private void BuildSettingsPanel()
    {
        settingsPanel = BuildSectionPanel("Settings Panel", settingsTitle, out float y);
        rotationToggle = AddToggleRow(settingsPanel.transform, rotationLabel, clicker == null || clicker.AllowRotation,
                                      on => { if (clicker != null) clicker.AllowRotation = on; }, ref y);
        pulsingToggle = AddToggleRow(settingsPanel.transform, pulsingLabel, clicker == null || clicker.AllowPulsing,
                                     on => { if (clicker != null) clicker.AllowPulsing = on; }, ref y);
        abbreviateToggle = AddToggleRow(settingsPanel.transform, abbreviateLabel, PixelClicker.AbbreviateNumbers,
                                        on => PixelClicker.AbbreviateNumbers = on, ref y);
        hidePurchasedToggle = AddToggleRow(settingsPanel.transform, hidePurchasedLabel, PixelShop.HidePurchased,
                                           on => PixelShop.HidePurchased = on, ref y);
        backgroundToggle = AddToggleRow(settingsPanel.transform, runInBackgroundLabel, clicker != null && clicker.RunInBackground,
                                        on => { if (clicker != null) clicker.RunInBackground = on; }, ref y);
        pauseStopsToggle = AddToggleRow(settingsPanel.transform, pauseStopsLabel, pauseStopsGame, on =>
        {
            pauseStopsGame = on;
            PlayerPrefs.SetInt(PrefPauseStops, on ? 1 : 0);
            ApplyFreeze(); // takes effect right away, even though the menu is open
        }, ref y);

        // Sound: three volume sliders and a mute box (the sound system is added by Start, which runs first).
        PixelAudio audio = PixelFind.First<PixelAudio>();
        if (audio != null)
        {
            masterSlider = AddSliderRow(settingsPanel.transform, masterVolumeLabel, audio.MasterVolume, v => audio.MasterVolume = v, ref y);
            effectsSlider = AddSliderRow(settingsPanel.transform, effectsVolumeLabel, audio.EffectsVolume, v => audio.EffectsVolume = v, ref y);
            musicSlider = AddSliderRow(settingsPanel.transform, musicVolumeLabel, audio.MusicVolume, v => audio.MusicVolume = v, ref y);
            muteToggle = AddToggleRow(settingsPanel.transform, muteLabel, audio.Muted, on => audio.Muted = on, ref y);
        }
        FinishSectionPanel(settingsPanel, y);
    }

    private string FormatCount(double value)
    {
        if (!PixelClicker.AbbreviateNumbers || value < 1000d) return System.Math.Floor(value).ToString("N0");
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
        ghostsValue.text = FormatCount(stats.GhostsClicked);
        meteorsClickedValue.text = FormatCount(stats.MeteorsClicked);
        meteorsSpawnedValue.text = FormatCount(stats.MeteorsSpawned);
        blackHolesValue.text = FormatCount(stats.BlackHolesSpawned);
        fansValue.text = FormatCount(stats.FansUsed);
        vacuumsValue.text = FormatCount(stats.VacuumDevicesUsed);
        comboValue.text = FormatCount(stats.HighestCombo);
        PixelUIKit.UpdateScrollView(statsScroll, statsBar, statsContentHeight, statsViewHeight);

        // Potions used: a button that opens a scrolling box.
        potionsHeaderLabel.text = string.Format(potionsUsedFormat, FormatCount(stats.TotalPotionsUsed), potionsOpen ? "-" : "+");
        if (potionsBox.activeSelf != potionsOpen) LayoutStats();
        if (potionsOpen)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (System.Collections.Generic.KeyValuePair<string, long> p in stats.PotionsUsed)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(string.Format(potionLineFormat, p.Key, FormatCount(p.Value)));
            }
            string text = sb.Length > 0 ? sb.ToString() : noPotionsText;
            if (text != shownPotionsText)
            {
                shownPotionsText = text;
                potionsText.text = text;
            }
            float width = potionsText.rectTransform.rect.width > 1f ? potionsText.rectTransform.rect.width : panelSize.x - 140f;
            float contentHeight = Mathf.Ceil(potionsText.GetPreferredValues(text, width, 0f).y) + 8f;
            potionsText.rectTransform.offsetMin = new Vector2(8f, -contentHeight);
            PixelUIKit.UpdateScrollView(potionsScroll, potionsBar, contentHeight, potionsBoxHeight - 16f);
        }
    }

    /// <summary>Two half-width buttons side by side on one row (keeps the menu short).</summary>
    /// <summary>
    /// Lays the main menu's buttons out in columns and sizes the panel to fit them: wider than tall, and (if Fit Between
    /// Bars is on) never taller than the space between the black bars - the buttons shrink if there are too many.
    /// </summary>
    private void LayoutMainButtons(Transform parent, List<MenuEntry> entries, float top, RectTransform panelRect)
    {
        int columns = Mathf.Max(1, mainColumns);
        int rows = Mathf.CeilToInt(entries.Count / (float)columns);
        float cellWidth = (mainPanelWidth - 80f - columnGap * (columns - 1)) / columns;

        float buttonHeight = menuButtonSize.y;
        float available = AvailableHeight();
        if (fitBetweenBars)
        {
            float room = available - top - 30f;
            float fitted = room / Mathf.Max(1, rows) - menuButtonSpacing;
            buttonHeight = Mathf.Clamp(Mathf.Min(buttonHeight, fitted), 40f, menuButtonSize.y);
        }

        for (int i = 0; i < entries.Count; i++)
        {
            int row = i / columns, col = i % columns;
            bool lastAlone = i == entries.Count - 1 && col == 0 && columns > 1;
            float width = lastAlone ? mainPanelWidth - 80f : cellWidth; // a lone last button spans the row
            float x = lastAlone ? 0f : -(mainPanelWidth - 80f) * 0.5f + cellWidth * 0.5f + col * (cellWidth + columnGap);

            Button button = MakeButton(parent, entries[i].label + " Button", entries[i].label, new Vector2(width, buttonHeight),
                                       entries[i].color, menuButtonFontSize);
            RectTransform rt = button.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(x, -(top + row * (buttonHeight + menuButtonSpacing)));
            button.onClick.AddListener(entries[i].action);
        }

        float needed = top + rows * (buttonHeight + menuButtonSpacing) + 10f;
        if (fitBetweenBars) needed = Mathf.Min(needed, available);
        panelRect.sizeDelta = new Vector2(mainPanelWidth, needed);
    }

    /// <summary>Height between the black bars (canvas units), minus the margin kept next to each bar.</summary>
    private float AvailableHeight()
    {
        float bar = PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f;
        return Mathf.Max(300f, referenceResolution.y - 2f * (bar + barMargin));
    }

    /// <summary>A scrolling text height that still leaves room for the title above and the Back button below, between the bars.</summary>
    private float FitViewHeight(float desired, float yTop)
    {
        if (!fitBetweenBars) return desired;
        float below = 10f + menuButtonSize.y + menuButtonSpacing + 30f; // Back button block
        return Mathf.Max(120f, Mathf.Min(desired, AvailableHeight() - yTop - below));
    }

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

/// <summary>
/// A button that only fires after the mouse button has been held down on it for a while. A fill grows across it as
/// feedback; letting go or moving off the button starts over. Uses unscaled time, so it works while the game is paused.
/// </summary>
public class PixelHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    private RectTransform fill;
    private float seconds = 2f;
    private System.Action onComplete;
    private bool holding;
    private float held;

    public void Setup(RectTransform fillRect, float holdSeconds, System.Action complete)
    {
        fill = fillRect;
        seconds = Mathf.Max(0.05f, holdSeconds);
        onComplete = complete;
        ResetProgress();
    }

    public void ResetProgress()
    {
        holding = false;
        held = 0f;
        Apply();
    }

    public void OnPointerDown(PointerEventData e) { holding = true; }
    public void OnPointerUp(PointerEventData e) { ResetProgress(); }
    public void OnPointerExit(PointerEventData e) { ResetProgress(); }

    private void Update()
    {
        if (!holding) return;
        held += Time.unscaledDeltaTime;
        Apply();
        if (held >= seconds)
        {
            holding = false;
            onComplete?.Invoke();
        }
    }

    private void Apply()
    {
        if (fill == null) return;
        fill.anchorMax = new Vector2(Mathf.Clamp01(held / seconds), 1f);
    }
}
