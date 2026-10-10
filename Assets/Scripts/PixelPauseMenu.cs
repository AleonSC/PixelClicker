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
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() // keeps static state clean when Enter Play Mode skips the domain reload
    {
        IsPaused = false;
        GameStopped = false;
    }

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

    [Tooltip("Restart button text (on the Stats screen).")]
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

    [Tooltip("Title of the box that opens when Quit is pressed.")]
    [SerializeField] private string quitChoiceTitle = "Quit to...";

    [Tooltip("Button text: leave the game.")]
    [SerializeField] private string quitDesktopText = "Desktop";

    [Tooltip("Button text: go back to the title screen.")]
    [SerializeField] private string quitMainMenuText = "Main menu";

    [Tooltip("Quit button text.")]
    [SerializeField] private string quitText = "Quit";

    [Tooltip("Add a Pixel Save Game component at startup if the scene has none.")]
    [SerializeField] private bool addSaveGameIfMissing = true;

    [Tooltip("Show the Save / Load button (it opens a window with the save slots).")]
    [SerializeField] private bool showSaveLoad = true;

    [Tooltip("Text of the main menu button that opens the save slots, and the title of that window.")]
    [SerializeField] private string saveLoadText = "Save / Load";

    [Tooltip("Slot name. {0} = the slot number.")]
    [SerializeField] private string slotNameFormat = "Slot {0}";

    [Tooltip("Added to the name of the slot the game is using right now (autosave goes there).")]
    [SerializeField] private string currentSlotTag = "  (current)";

    [Tooltip("Fourth line of a slot that has a save. {0} = pixels collected in total.")]
    [SerializeField] private string slotPixelsFormat = "{0} pixels";
    [Tooltip("Third line of a slot that has a save. {0} = total time played.")]
    [SerializeField] private string slotTimeFormat = "Total time played: {0}";

    [Tooltip("Second line of a slot with no save.")]
    [SerializeField] private string emptySlotText = "Empty";

    [Tooltip("Title of the overwrite warning. {0} = the slot number.")]
    [SerializeField] private string overwriteTitle = "Overwrite slot {0}?";

    [Tooltip("Text of the overwrite warning. {0} = the slot number, {1} = when that save was made.")]
    [SerializeField] private string overwriteMessage = "Slot {0} already has a save from {1}. Saving now replaces it and it cannot be restored.";

    [Tooltip("Button that confirms the overwrite.")]
    [SerializeField] private string overwriteConfirmText = "Overwrite";

    [Tooltip("Button that cancels the overwrite.")]
    [SerializeField] private string overwriteCancelText = "Cancel";

    [Min(40f)]
    [Tooltip("Height of one slot row in the Save / Load window (canvas units).")]
    [SerializeField] private float slotHeight = 96f;

    [Range(2, 8)]
    [Tooltip("How many slot rows show at once; more slots scroll.")]
    [SerializeField] private int visibleSlotRows = 5;

    [Tooltip("Slot row colour.")]
    [SerializeField] private Color slotColor = new Color(0.22f, 0.22f, 0.28f, 1f);

    [Tooltip("Colour of the selected slot.")]
    [SerializeField] private Color slotSelectedColor = new Color(0.25f, 0.5f, 0.75f, 1f);

    [Tooltip("Colour of the overwrite warning text and button.")]
    [SerializeField] private Color overwriteWarningColor = new Color(0.9f, 0.35f, 0.3f, 1f);

    [Header("Save / Load result window")]
    [Tooltip("Colour of the window that fills the Save / Load menu after saving or loading.")]
    [SerializeField] private Color resultColor = new Color(0.16f, 0.36f, 0.78f, 1f);

    [Tooltip("Button on the result window.")]
    [SerializeField] private string resultButtonText = "Got it";

    [Tooltip("Result text after a save. {0} = the slot number.")]
    [SerializeField] private string savedResultFormat = "Saved to slot {0}";

    [Tooltip("Result text when saving failed.")]
    [SerializeField] private string saveFailedResultText = "Save failed";

    [Tooltip("Result text after a load. {0} = the slot number.")]
    [SerializeField] private string loadedResultFormat = "Loaded slot {0}";

    [Tooltip("Result text when loading failed.")]
    [SerializeField] private string loadFailedResultText = "Load failed";

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

    [Min(200f)]
    [Tooltip("Height of the scrolling Settings list (canvas units). It is shortened to fit between the black bars.")]
    [SerializeField] private float settingsViewHeight = 640f;

    [Header("Settings Screen Rows")]
    [SerializeField] private string headerGameplay = "Gameplay";
    [SerializeField] private string headerInterface = "Interface";
    [SerializeField] private string headerSaving = "Saving";
    [SerializeField] private string headerDisplay = "Display";
    [SerializeField] private string headerAudio = "Audio";
    [SerializeField] private string headerControls = "Controls";
    [SerializeField] private string tipsLabel = "Show first-time tips";
    [SerializeField] private string replayTutorialText = "Replay tutorial";
    [SerializeField] private string eventLogLabel = "Event log";
    [SerializeField] private string eventLogSizeLabel = "Event log text size";
    [SerializeField] private string popupsLabel = "Number popups (+N)";
    [SerializeField] private string popupSizeLabel = "Popup size";
    [SerializeField] private string uiScaleLabel = "UI scale (after restart)";
    [SerializeField] private string colorBlindLabel = "Colour-blind marks";
    [SerializeField] private string cameraIntroLabel = "Camera intro (next start)";
    [SerializeField] private string autoSaveLabel = "Autosave every";
    [SerializeField] private string autoSaveMessageLabel = "Show autosave message";
    [SerializeField] private string qualityLabel = "Graphics quality";
    [SerializeField] private string vsyncLabel = "VSync";
    [SerializeField] private string fullscreenLabel = "Fullscreen";
    [SerializeField] private string resolutionLabel = "Resolution";
    [SerializeField] private string keyBindingsText = "Key bindings...";
    [SerializeField] private string keyBindingsTitle = "Key Bindings";
    [SerializeField] private string resetKeysText = "Reset keys to default";
    [SerializeField] private string pressAKeyText = "Press a key...";

    [Tooltip("Names of the rebindable actions, in the order of PixelAction (Time Stop, Hose, Turn left, Turn right, Event log, Time slow).")]
    [SerializeField] private string[] keyActionLabels = { "Time Stop", "Pixel Bank hose", "Turn left (fan / sorter)", "Turn right (fan / sorter)", "Event log", "Time slow" };

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

    [Tooltip("Label of the floor style picker (arrows step through the styles of the Pixel Floor component).")]
    [SerializeField] private string floorStyleLabel = "Floor style";

    [Tooltip("Label of the sky picker (arrows step through the skies of the Pixel Skybox component).")]
    [SerializeField] private string skyStyleLabel = "Sky";

    [Tooltip("Label of the tick box that softens the floor's far edge with fog.")]
    [SerializeField] private string horizonFogLabel = "Horizon fog";

    [Tooltip("Label of the Settings tick box for the bevelled button look (takes effect for windows built after the next start).")]
    [SerializeField] private string fancyButtonsLabel = "Fancy buttons (next start)";

    [Tooltip("Label of the Settings tick box for the rounded, glowing window frames (takes effect for windows built after the next start).")]
    [SerializeField] private string fancyWindowsLabel = "Fancy windows (next start)";

    [Tooltip("Label of the Settings slider that picks the colour (hue) of the neon bar glow.")]
    [SerializeField] private string glowHueLabel = "Bar glow colour";

    [Tooltip("Label of the Settings tick box that lets the neon bar glow slowly change colour by itself.")]
    [SerializeField] private string glowCycleLabel = "Cycle glow colour";

    [Tooltip("Label of the Settings tick box that switches on your own window colour.")]
    [SerializeField] private string uiColorLabel = "Custom UI colour";

    [Tooltip("Label of the Settings slider that picks the hue of the windows.")]
    [SerializeField] private string uiHueLabel = "UI colour";

    [Tooltip("Label of the Settings tick box for the neon glow along the black bars.")]
    [SerializeField] private string barGlowLabel = "Neon bar glow";

    [Tooltip("Shown in the floor style picker when the scene has no floor to restyle.")]
    [SerializeField] private string noFloorText = "No floor found";

    [Tooltip("Colour of the floor style and sky pickers' arrow buttons.")]
    [SerializeField] private Color floorArrowColor = new Color(0.25f, 0.25f, 0.3f, 1f);

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

    [Min(100f)]
    [Tooltip("Height of the scrolling stats list (canvas units). More stats than fit scroll.")]
    [SerializeField] private float statsViewHeight = 480f;

    [Tooltip("Scroll bar colour in the Stats screen.")]
    [SerializeField] private Color scrollbarColor = new Color(1f, 1f, 1f, 0.35f);

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

    /// <summary>Reads the "pause stops the game" setting again (a save file with its own settings was loaded).</summary>
    public void ReloadSettingsFromPrefs()
    {
        pauseStopsGame = PlayerPrefs.GetInt(PrefPauseStops, 1) != 0;
    }
    private bool pauseStopsGame = true;

    private GameObject canvasRoot;
    private GameObject menuRoot;
    private GameObject mainPanel, statsPanel, settingsPanel, changelogPanel, restartPanel, quitPanel;
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
        public bool fullRow; // spans the whole row (Resume, Quit)
    }
    private ScrollRect statsScroll;
    private GameObject statsBar;
    private RectTransform statsContent;
    private readonly List<TMP_Text> statValueTexts = new List<TMP_Text>();
    private string statsSignature = "";
    private RectTransform statsPanelRect, statsRestartRect, statsBackRect;
    private float statsContentHeight, statsListTop;
    private GameObject keysPanel;
    private readonly List<System.Action> settingsRefreshers = new List<System.Action>();
    private TMP_Text[] keyButtonLabels;
    private int capturingAction = -1;
    private GameObject saveLoadPanel, overwriteOverlay;
    private TMP_Text overwriteText;
    private Button[] slotButtons;
    private TMP_Text[] slotLabels;
    private Button slotSaveButton, slotLoadButton;
    private ScrollRect slotScroll;
    private GameObject slotBar;
    private int selectedSlot = 1;
    private Toggle rotationToggle, pulsingToggle, abbreviateToggle, hidePurchasedToggle, backgroundToggle, pauseStopsToggle;
    private float previousTimeScale = 1f;

    private void Start()
    {
        if (clicker == null)
        {
            clicker = PixelFind.First<PixelClicker>();
        }
        if (clicker != null && clicker.UIFont != null) font = clicker.UIFont; // one shared font

        ReloadSettingsFromPrefs();

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
        if (IsPaused && UpdateKeyCapture()) return;

        if (EscapePressed())
        {
            if (IsPaused && PixelDevTools.ClosePanel())
            {
                // The dev tools panel (opened from this menu) took the key press.
            }
            else if (IsPaused)
            {
                // Inside Stats / Settings, Escape steps back; from the main view it resumes.
                if (resultOverlay != null && resultOverlay.activeSelf) resultOverlay.SetActive(false);
                else if (overwriteOverlay != null && overwriteOverlay.activeSelf) overwriteOverlay.SetActive(false);
                else if (mainPanel != null && !mainPanel.activeSelf) ShowView(mainPanel);
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
        if (statsPanel != null && statsPanel.activeSelf && Time.unscaledTime >= nextStatsRefresh)
        {
            nextStatsRefresh = Time.unscaledTime + 0.25f;
            RefreshStats();
        }
    }

    private float nextStatsRefresh;

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
        if (!paused) openedFromTitle = false;

        ApplyFreeze();

        if (menuRoot != null) menuRoot.SetActive(paused);
        if (paused) ShowView(mainPanel);
    }

    private bool openedFromTitle;

    /// <summary>Opens the Settings screen straight from the title screen (Back / Escape return to the title).</summary>
    public static void OpenSettingsFromTitle() { PixelFind.First<PixelPauseMenu>()?.OpenFromTitle(true); }

    /// <summary>Opens the Save / Load window straight from the title screen.</summary>
    public static void OpenLoadFromTitle() { PixelFind.First<PixelPauseMenu>()?.OpenFromTitle(false); }

    private void OpenFromTitle(bool settings)
    {
        if (IsPaused) return;
        SetPaused(true);
        openedFromTitle = true;
        ShowView(settings ? settingsPanel : saveLoadPanel);
    }

    /// <summary>Freezes or unfreezes time to match 'menu open' and the 'Pausing stops the game' setting.</summary>
    private void ApplyFreeze()
    {
        bool freeze = IsPaused && pauseStopsGame && !PixelTitleScreen.Showing; // the title screen already holds time at 0

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
        if (pauseKey != PauseKey.Escape && PixelKeys.Typing) return false;   // typing a P / Tab / Backspace in a text box must not pause
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
            PixelHints.ResetSeen(); // a new game shows the first-time tips (and the intro) again
        }
        PixelCrashLog.EndSessionCleanly(); // a deliberate restart is not a crash
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
        PixelUIKit.StyleWindow(panel.GetComponent<Image>(), panelColor);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = panelSize;
        panelRect.anchoredPosition = Vector2.zero;

        TMP_Text title = MakeText(panel.transform, "Title", menuTitle, titleFontSize, FontStyles.Bold);
        PixelUIKit.Caps(title);
        RectTransform tr = title.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f);
        tr.anchorMax = new Vector2(1f, 1f);
        tr.pivot = new Vector2(0.5f, 1f);
        tr.sizeDelta = new Vector2(0f, titleFontSize * 1.6f);
        tr.anchoredPosition = new Vector2(0f, -30f);

        float y = 30f + titleFontSize * 1.6f + 20f;
        List<MenuEntry> entries = new List<MenuEntry>();

        // Build every screen first, then list the buttons in the order they appear (two per row).
        if (showSaveLoad && saveGame != null) BuildSaveLoadPanel();
        BuildStatsPanel();
        BuildSettingsPanel();
        BuildQuitPanel();
        BuildChangelogPanel();
        BuildRestartPanel();
        if (showHowToPlay) howToPlayScreen = BuildGuideScreen("How To Play Panel", howToPlayText, howToPlayResource, emptyHowToPlayText);
        if (showControls) controlsScreen = BuildGuideScreen("Controls Panel", controlsText, controlsResource, emptyControlsText);

        // Resume on top, alone
        entries.Add(new MenuEntry { label = resumeText, color = menuButtonColor, action = () => SetPaused(false), fullRow = true });
        // Save / Load | Stats
        if (saveLoadPanel != null) entries.Add(new MenuEntry { label = saveLoadText, color = menuButtonColor, action = () => ShowView(saveLoadPanel) });
        if (showStats) entries.Add(new MenuEntry { label = statsText, color = menuButtonColor, action = () => ShowView(statsPanel) });
        // Settings | Controls
        if (showSettings) entries.Add(new MenuEntry { label = settingsText, color = menuButtonColor, action = () => ShowView(settingsPanel) });
        if (controlsScreen != null) entries.Add(new MenuEntry { label = controlsText, color = menuButtonColor, action = () => ShowView(controlsScreen.panel) });
        // How to Play | Changelog
        if (howToPlayScreen != null) entries.Add(new MenuEntry { label = howToPlayText, color = menuButtonColor, action = () => ShowView(howToPlayScreen.panel) });
        if (showChangelog) entries.Add(new MenuEntry { label = changelogText, color = menuButtonColor, action = () => ShowView(changelogPanel) });
        // Quit at the bottom, alone
        if (showQuit) entries.Add(new MenuEntry { label = quitText, color = quitButtonColor, action = () => ShowView(quitPanel), fullRow = true });

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
        if (openedFromTitle && view == mainPanel) { SetPaused(false); return; } // 'back' returns to the title screen
        if (mainPanel != null) mainPanel.SetActive(view == mainPanel);
        if (statsPanel != null) statsPanel.SetActive(view == statsPanel);
        if (settingsPanel != null) settingsPanel.SetActive(view == settingsPanel);
        if (changelogPanel != null) changelogPanel.SetActive(view == changelogPanel);
        if (howToPlayScreen != null) howToPlayScreen.panel.SetActive(view == howToPlayScreen.panel);
        if (controlsScreen != null) controlsScreen.panel.SetActive(view == controlsScreen.panel);
        if (howToPlayScreen != null && view == howToPlayScreen.panel) RefreshGuideScreen(howToPlayScreen);
        if (controlsScreen != null && view == controlsScreen.panel) RefreshGuideScreen(controlsScreen);
        if (restartPanel != null) restartPanel.SetActive(view == restartPanel);
        if (quitPanel != null) quitPanel.SetActive(view == quitPanel);
        if (saveLoadPanel != null) saveLoadPanel.SetActive(view == saveLoadPanel);
        if (keysPanel != null) keysPanel.SetActive(view == keysPanel);
        if (view == keysPanel) { capturingAction = -1; RefreshKeyLabels(); }
        if (view == saveLoadPanel) OpenSaveLoad();
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
        if (view == settingsPanel) foreach (System.Action refresh in settingsRefreshers) refresh();
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
        PixelUIKit.StyleWindow(panel.GetComponent<Image>(), panelColor);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = panelSize;
        rect.anchoredPosition = Vector2.zero;

        TMP_Text t = MakeText(panel.transform, "Title", title, titleFontSize, FontStyles.Bold);
        PixelUIKit.Caps(t);
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
    private void FinishSectionPanel(GameObject panel, float y, GameObject backTo = null)
    {
        y += 10f;
        AddMenuButton(panel.transform, backText, menuButtonColor, ref y, () => ShowView(backTo != null ? backTo : mainPanel));

        RectTransform rect = panel.GetComponent<RectTransform>();
        float needed = y + 30f;
        if (needed > rect.sizeDelta.y) rect.sizeDelta = new Vector2(rect.sizeDelta.x, needed);
        panel.SetActive(false);
    }

    private TMP_Text AddRowLabel(Transform parent, string text, float y, out RectTransform row)
    {
        // "(after restart)" / "(next start)" / "(requires restart)" is taken off the name and shown as a hover tip instead.
        bool needsRestart = false;
        System.Text.RegularExpressions.Match m = System.Text.RegularExpressions.Regex.Match(text, @"\s*\((after restart|next start|requires restart)\)\s*$",
                                                                                         System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (m.Success) { text = text.Substring(0, m.Index); needsRestart = true; }
        GameObject go = new GameObject(text + " Row", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        row = go.GetComponent<RectTransform>();
        row.anchorMin = new Vector2(0f, 1f);
        row.anchorMax = new Vector2(1f, 1f);
        row.pivot = new Vector2(0.5f, 1f);
        row.sizeDelta = new Vector2(-80f, rowHeight);
        row.anchoredPosition = new Vector2(0f, -y);

        TMP_Text label = MakeText(go.transform, "Label", text, rowFontSize * 0.65f, FontStyles.Normal);   // one size for every row so they line up
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.enableAutoSizing = false;
        label.fontSize = rowFontSize * 0.65f;
        label.overflowMode = TextOverflowModes.Overflow;
#if UNITY_2023_1_OR_NEWER
        label.textWrappingMode = TextWrappingModes.NoWrap;   // ONE line: a long name shrinks to fit instead of wrapping
#else
        label.enableWordWrapping = false;
#endif
        RectTransform lr = label.rectTransform;
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = new Vector2(0.62f, 1f);
        lr.offsetMin = lr.offsetMax = Vector2.zero;
        if (needsRestart)
        {
            Image hit = go.AddComponent<Image>();   // invisible, only so the whole row reacts to the mouse
            hit.color = new Color(0f, 0f, 0f, 0f);
            AddSettingTip(go, restartTipText);
        }
        return label;
    }

    [Tooltip("Hover tip on Settings rows whose change only applies after the game is restarted.")]
    [SerializeField] private string restartTipText = "Requires a restart to take effect.";

    private RectTransform settingTipRect;
    private TMP_Text settingTipLabel;

    /// <summary>A small box next to the mouse while it is over 'target' (the Settings screen's own hover tip).</summary>
    private void AddSettingTip(GameObject target, string text)
    {
        if (settingTipRect == null)
        {
            GameObject box = new GameObject("Setting Tip", typeof(RectTransform), typeof(Image), typeof(PixelDevTip));
            box.transform.SetParent(canvasRoot.transform, false);
            Image bg = box.GetComponent<Image>();
            bg.color = new Color(0.05f, 0.06f, 0.09f, 0.96f);
            bg.raycastTarget = false;
            PixelUIKit.StyleBox(bg);
            settingTipRect = box.GetComponent<RectTransform>();
            settingTipRect.anchorMin = settingTipRect.anchorMax = new Vector2(0.5f, 0.5f);
            settingTipLabel = MakeText(box.transform, "Text", "", rowFontSize * 0.6f, FontStyles.Normal);
            settingTipLabel.raycastTarget = false;
            PixelUIKit.Stretch(settingTipLabel.rectTransform);
            settingTipLabel.rectTransform.offsetMin = new Vector2(12f, 8f);
            settingTipLabel.rectTransform.offsetMax = new Vector2(-12f, -8f);
            PixelDevTip follow = box.GetComponent<PixelDevTip>();
            follow.canvasRect = canvasRoot.GetComponent<RectTransform>();
            follow.rect = settingTipRect;
            box.SetActive(false);
        }
        PixelHoverTip hover = target.AddComponent<PixelHoverTip>();
        hover.onEnter = () =>
        {
            settingTipLabel.text = text;
            Vector2 pref = settingTipLabel.GetPreferredValues(text, 520f, 0f);
            settingTipRect.sizeDelta = new Vector2(Mathf.Min(520f, pref.x) + 24f, pref.y + 16f);
            settingTipRect.gameObject.SetActive(true);
            settingTipRect.SetAsLastSibling();
        };
        hover.onExit = () => { if (settingTipRect != null) settingTipRect.gameObject.SetActive(false); };
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
        TMP_Text toggleLabel = AddRowLabel(parent, label, y, out RectTransform row);
        toggleLabel.rectTransform.anchorMax = Vector2.one;                                   // the name may run right up to the tick box
        toggleLabel.rectTransform.offsetMax = new Vector2(-(tickBoxSize + 14f), 0f);

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

        // The stats scroll when there are more than fit.
        statsScroll = PixelUIKit.CreateScrollView(statsPanel.transform, "Stats List", scrollbarColor, 12f, rowHeight,
                                                  out RectTransform content, out statsBar);
        RectTransform vr = statsScroll.GetComponent<RectTransform>();
        vr.anchorMin = new Vector2(0f, 1f);
        vr.anchorMax = new Vector2(1f, 1f);
        vr.pivot = new Vector2(0.5f, 1f);
        vr.sizeDelta = new Vector2(0f, statsViewHeight);
        vr.anchoredPosition = new Vector2(0f, -y);

        statsContent = content;
        RebuildStatRows(); // the rows come from PixelStats.GetLines (they change as pixels unlock and minigames start)

        // Restart lives here now (it opens the hold-to-confirm screen).
        if (showRestart)
        {
            Button restart = MakeButton(statsPanel.transform, "Restart Button", restartText, menuButtonSize, quitButtonColor, menuButtonFontSize);
            statsRestartRect = restart.GetComponent<RectTransform>();
            statsRestartRect.anchorMin = statsRestartRect.anchorMax = statsRestartRect.pivot = new Vector2(0.5f, 1f);
            restart.onClick.AddListener(() => ShowView(restartPanel));
        }

        // Back button (moves with the section)
        Button back = MakeButton(statsPanel.transform, "Back Button", backText, menuButtonSize, menuButtonColor, menuButtonFontSize);
        statsBackRect = back.GetComponent<RectTransform>();
        statsBackRect.anchorMin = statsBackRect.anchorMax = statsBackRect.pivot = new Vector2(0.5f, 1f);
        back.onClick.AddListener(() => ShowView(mainPanel));

        LayoutStats();
        statsPanel.SetActive(false);
    }

    /// <summary>Places the Restart and Back buttons under the stats list and sizes the Stats panel.</summary>
    private void LayoutStats()
    {
        float y = statsListTop + statsViewHeight + 12f;
        if (statsRestartRect != null)
        {
            statsRestartRect.anchoredPosition = new Vector2(0f, -y);
            y += menuButtonSize.y + menuButtonSpacing;
        }

        statsBackRect.anchoredPosition = new Vector2(0f, -y);
        y += menuButtonSize.y + 30f;
        statsPanelRect.sizeDelta = new Vector2(panelSize.x, Mathf.Max(panelSize.y, y));
    }

    // ------------------------------------------------------------------
    // Save / Load window (save slots)
    // ------------------------------------------------------------------

    private void BuildSaveLoadPanel()
    {
        int count = saveGame.SlotCount;
        saveLoadPanel = BuildSectionPanel("Save Load Panel", saveLoadText, out float y);

        // The slot list (scrolls when there are more slots than rows).
        float rowH = Mathf.Max(slotHeight, 150f);   // name + date + pixels + time played
        float rowStep = rowH + 8f;
        float viewHeight = FitViewHeight(Mathf.Min(count, visibleSlotRows) * rowStep, y + menuButtonSize.y + 14f);   // leaves room for the Save / Load buttons
        slotScroll = PixelUIKit.CreateScrollView(saveLoadPanel.transform, "Slot List", scrollbarColor, 12f, rowStep,
                                                 out RectTransform content, out slotBar);
        RectTransform vr = slotScroll.GetComponent<RectTransform>();
        vr.anchorMin = new Vector2(0f, 1f);
        vr.anchorMax = new Vector2(1f, 1f);
        vr.pivot = new Vector2(0.5f, 1f);
        vr.sizeDelta = new Vector2(-40f, viewHeight);
        vr.anchoredPosition = new Vector2(0f, -y);

        slotButtons = new Button[count];
        slotLabels = new TMP_Text[count];
        for (int i = 0; i < count; i++)
        {
            int slot = i + 1;
            Button b = MakeButton(content, "Slot " + slot, "", new Vector2(panelSize.x - 100f, rowH), slotColor, rowFontSize);
            RectTransform rt = b.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(-8f, -(i * rowStep));
            b.onClick.AddListener(() => SelectSlot(slot));

            TMP_Text label = b.GetComponentInChildren<TMP_Text>();
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.richText = true;
            label.enableAutoSizing = true; // two lines always fit the row: the text shrinks instead of overflowing
            label.fontSizeMax = rowFontSize * 0.85f;
            label.fontSizeMin = 10f;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.rectTransform.offsetMin = new Vector2(20f, 6f);
            label.rectTransform.offsetMax = new Vector2(-12f, -6f);
            slotButtons[i] = b;
            slotLabels[i] = label;
        }
        PixelUIKit.UpdateScrollView(slotScroll, slotBar, count * rowStep, viewHeight);
        y += viewHeight + 14f;

        // Save | Load for the selected slot.
        float gap = 12f;
        Vector2 half = new Vector2((panelSize.x - 80f - gap) * 0.5f, menuButtonSize.y);
        slotSaveButton = MakeButton(saveLoadPanel.transform, "Slot Save", saveText, half, menuButtonColor, menuButtonFontSize);
        slotLoadButton = MakeButton(saveLoadPanel.transform, "Slot Load", loadText, half, menuButtonColor, menuButtonFontSize);
        RectTransform sr = slotSaveButton.GetComponent<RectTransform>(), lr = slotLoadButton.GetComponent<RectTransform>();
        sr.anchorMin = sr.anchorMax = sr.pivot = new Vector2(0.5f, 1f);
        lr.anchorMin = lr.anchorMax = lr.pivot = new Vector2(0.5f, 1f);
        sr.anchoredPosition = new Vector2(-(half.x + gap) * 0.5f, -y);
        lr.anchoredPosition = new Vector2((half.x + gap) * 0.5f, -y);
        slotSaveButton.onClick.AddListener(OnSlotSave);
        slotLoadButton.onClick.AddListener(OnSlotLoad);
        y += menuButtonSize.y + 14f;

        BuildOverwriteOverlay(saveLoadPanel.transform);
        BuildResultOverlay(saveLoadPanel.transform);
        FinishSectionPanel(saveLoadPanel, y);
        overwriteOverlay.transform.SetAsLastSibling(); // above the Back button too
        resultOverlay.transform.SetAsLastSibling();
    }

    /// <summary>The "this slot already has a save" warning: a dimmed box over the window with Overwrite / Cancel.</summary>
    private void BuildOverwriteOverlay(Transform parent)
    {
        overwriteOverlay = new GameObject("Overwrite Warning", typeof(RectTransform), typeof(Image));
        overwriteOverlay.transform.SetParent(parent, false);
        overwriteOverlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f); // also blocks the clicks behind it
        PixelUIKit.Stretch(overwriteOverlay.GetComponent<RectTransform>());

        GameObject box = new GameObject("Box", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(overwriteOverlay.transform, false);
        PixelUIKit.StyleWindow(box.GetComponent<Image>(), panelColor);
        RectTransform br = box.GetComponent<RectTransform>();
        br.anchorMin = br.anchorMax = br.pivot = new Vector2(0.5f, 0.5f);
        br.sizeDelta = new Vector2(panelSize.x - 30f, 460f);

        TMP_Text title = MakeText(box.transform, "Title", "", titleFontSize * 0.8f, FontStyles.Bold);
        title.color = overwriteWarningColor;
        RectTransform tr = title.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f); tr.pivot = new Vector2(0.5f, 1f);
        tr.sizeDelta = new Vector2(0f, titleFontSize * 1.3f);
        tr.anchoredPosition = new Vector2(0f, -20f);
        title.enableAutoSizing = true; // long titles shrink instead of overflowing
        title.fontSizeMax = titleFontSize * 0.8f;
        title.fontSizeMin = 16f;
        overwriteTitleLabel = title;

        overwriteText = MakeText(box.transform, "Message", "", rowFontSize * 0.9f, FontStyles.Normal);
        overwriteText.alignment = TextAlignmentOptions.Top;
        RectTransform mr = overwriteText.rectTransform;
        mr.anchorMin = Vector2.zero; mr.anchorMax = Vector2.one; mr.pivot = new Vector2(0.5f, 0.5f);
        mr.offsetMin = new Vector2(30f, 24f + menuButtonSize.y + 20f);               // above the buttons
        mr.offsetMax = new Vector2(-30f, -(20f + titleFontSize * 1.3f + 10f));        // below the title
        overwriteText.enableAutoSizing = true; // the message shrinks to fit its area
        overwriteText.fontSizeMax = rowFontSize * 0.9f;
        overwriteText.fontSizeMin = 14f;
        overwriteText.overflowMode = TextOverflowModes.Ellipsis;

        float gap = 12f;
        Vector2 half = new Vector2((br.sizeDelta.x - 60f - gap) * 0.5f, menuButtonSize.y);
        Button confirm = MakeButton(box.transform, "Overwrite", overwriteConfirmText, half, overwriteWarningColor, menuButtonFontSize);
        Button cancel = MakeButton(box.transform, "Cancel", overwriteCancelText, half, menuButtonColor, menuButtonFontSize);
        RectTransform cr = confirm.GetComponent<RectTransform>(), xr = cancel.GetComponent<RectTransform>();
        foreach (Button b in new[] { confirm, cancel })
        {
            TMP_Text bl = b.GetComponentInChildren<TMP_Text>();
            bl.enableAutoSizing = true; // "Overwrite" never wraps
            bl.fontSizeMax = menuButtonFontSize * 0.8f;
            bl.fontSizeMin = 12f;
            bl.overflowMode = TextOverflowModes.Ellipsis;
        }
        cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(0.5f, 0f);
        xr.anchorMin = xr.anchorMax = xr.pivot = new Vector2(0.5f, 0f);
        cr.anchoredPosition = new Vector2(-(half.x + gap) * 0.5f, 24f);
        xr.anchoredPosition = new Vector2((half.x + gap) * 0.5f, 24f);
        confirm.onClick.AddListener(() =>
        {
            overwriteOverlay.SetActive(false);
            bool saved = saveGame.SaveToSlot(selectedSlot, false);
            RefreshSlots();
            ShowResult(saved ? string.Format(savedResultFormat, selectedSlot) : saveFailedResultText);
        });
        cancel.onClick.AddListener(() => overwriteOverlay.SetActive(false));

        overwriteOverlay.SetActive(false);
    }

    private TMP_Text overwriteTitleLabel;
    private GameObject resultOverlay;
    private TMP_Text resultLabel;

    /// <summary>A blue window the size of the Save / Load menu that says what happened ("Saved to slot 2") with a Got it button.</summary>
    private void BuildResultOverlay(Transform parent)
    {
        resultOverlay = new GameObject("Result Window", typeof(RectTransform), typeof(Image));
        resultOverlay.transform.SetParent(parent, false);
        resultOverlay.GetComponent<Image>().color = resultColor; // opaque: covers the whole menu and blocks the clicks behind it
        PixelUIKit.Stretch(resultOverlay.GetComponent<RectTransform>());

        resultLabel = MakeText(resultOverlay.transform, "Message", "", titleFontSize, FontStyles.Bold);
        resultLabel.alignment = TextAlignmentOptions.Center;
        resultLabel.enableAutoSizing = true;
        resultLabel.fontSizeMax = titleFontSize;
        resultLabel.fontSizeMin = 18f;
        RectTransform lr = resultLabel.rectTransform;
        lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.pivot = new Vector2(0.5f, 0.5f);
        lr.offsetMin = new Vector2(40f, 24f + menuButtonSize.y + 30f);
        lr.offsetMax = new Vector2(-40f, -40f);

        Button ok = MakeButton(resultOverlay.transform, "Got It", resultButtonText, menuButtonSize, menuButtonColor, menuButtonFontSize);
        RectTransform or = ok.GetComponent<RectTransform>();
        or.anchorMin = or.anchorMax = or.pivot = new Vector2(0.5f, 0f);
        or.anchoredPosition = new Vector2(0f, 24f);
        ok.onClick.AddListener(() => resultOverlay.SetActive(false));
        resultOverlay.SetActive(false);
    }

    private void ShowResult(string text)
    {
        if (resultOverlay == null) return;
        resultLabel.text = text;
        resultOverlay.SetActive(true);
        resultOverlay.transform.SetAsLastSibling();
    }

    private void OpenSaveLoad()
    {
        selectedSlot = saveGame != null ? saveGame.CurrentSlot : 1;
        if (overwriteOverlay != null) overwriteOverlay.SetActive(false);
        if (resultOverlay != null) resultOverlay.SetActive(false);
        RefreshSlots();
    }

    private void SelectSlot(int slot)
    {
        selectedSlot = slot;
        RefreshSlots();
    }

    /// <summary>Fills every slot row (name, date, pixels) and marks the selected one; Load only works on a slot with a save.</summary>
    private void RefreshSlots()
    {
        if (saveGame == null || slotButtons == null) return;
        int current = saveGame.CurrentSlot;
        for (int i = 0; i < slotButtons.Length; i++)
        {
            int slot = i + 1;
            string name = string.Format(slotNameFormat, slot) + (slot == current ? currentSlotTag : "");
            string detail = saveGame.TryGetSlotInfo(slot, out string savedAt, out double pixels, out double played)
                ? savedAt + "\n" + string.Format(slotTimeFormat, PixelStats.FormatTime(played)) + "\n" + string.Format(slotPixelsFormat, FormatCount(pixels))
                : emptySlotText;
            slotLabels[i].text = "<b>" + name + "</b>\n<size=78%>" + detail + "</size>";
            slotButtons[i].GetComponent<Image>().color = slot == selectedSlot ? slotSelectedColor : slotColor;
        }
        slotLoadButton.interactable = saveGame.SlotHasSave(selectedSlot);
    }

    private void OnSlotSave()
    {
        if (saveGame == null) return;
        if (!saveGame.SlotHasSave(selectedSlot))
        {
            bool saved = saveGame.SaveToSlot(selectedSlot, false);
            RefreshSlots();
            ShowResult(saved ? string.Format(savedResultFormat, selectedSlot) : saveFailedResultText);
            return;
        }

        // Saving over an existing save: ask first.
        saveGame.TryGetSlotInfo(selectedSlot, out string savedAt, out double _);
        overwriteTitleLabel.text = string.Format(overwriteTitle, selectedSlot);
        overwriteText.text = string.Format(overwriteMessage, selectedSlot, savedAt);
        overwriteOverlay.SetActive(true);
    }

    private void OnSlotLoad()
    {
        if (saveGame == null) return;
        bool loaded = saveGame.LoadSlot(selectedSlot, false);
        RefreshSlots();
        ShowResult(loaded ? string.Format(loadedResultFormat, selectedSlot) : loadFailedResultText);
    }

    /// <summary>"This deletes everything" screen: the player has to HOLD the red button to confirm.</summary>
    private void BuildQuitPanel()
    {
        quitPanel = BuildSectionPanel("Quit Panel", quitChoiceTitle, out float y);
        AddMenuButton(quitPanel.transform, quitDesktopText, quitButtonColor, ref y, Quit);
        AddMenuButton(quitPanel.transform, quitMainMenuText, menuButtonColor, ref y, ToMainMenu);
        FinishSectionPanel(quitPanel, y);
    }

    /// <summary>Quit > Main menu: saves, then the camera zooms back into the cube and the title screen returns.</summary>
    private void ToMainMenu()
    {
        if (saveGame != null) saveGame.Save(false);
        SetPaused(false);
        PixelTitleScreen.ReturnToMenu();
    }

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

        FinishSectionPanel(restartPanel, y, statsPanel);
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

        FinishSectionPanel(s.panel, y + s.viewHeight);
        return s;
    }

    /// <summary>
    /// Reloads the screen's text file and shows it as rows: lines starting with // are skipped, # is a coloured heading,
    /// "Key | Action" is two columns (the action wraps inside its own column, so rows never overlap), anything else is plain text.
    /// </summary>
    private void RefreshGuideScreen(GuideScreen s)
    {
        TextAsset asset = Resources.Load<TextAsset>(s.resource);
        string raw = PixelKeys.Replace(asset != null ? asset.text : "");
        RectTransform content = s.scroll.content;
        for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);

        float viewWidth = s.scroll.GetComponent<RectTransform>().rect.width;
        if (viewWidth < 100f) viewWidth = guideWidth - 60f;
        float width = viewWidth - 36f;
        float keyWidth = width * Mathf.Clamp(guideColumnPercent, 15f, 60f) / 100f;
        float gap = 14f;
        float actionWidth = width - keyWidth - gap;

        float y = 0f;
        bool any = false;
        foreach (string rawLine in raw.Replace("\r", "").Split('\n'))
        {
            string line = rawLine.TrimEnd();
            string trimmed = line.TrimStart();
            if (trimmed.StartsWith("//")) continue;

            if (trimmed.StartsWith("#"))
            {
                if (any) y += 18f;   // air above each heading
                float size = guideFontSize * guideHeadingPercent / 100f;
                TMP_Text h = GuideCell(content, trimmed.TrimStart('#').Trim(), size, FontStyles.Bold, guideHeadingColor, 10f, width, y);
                y += Mathf.Ceil(h.GetPreferredValues(h.text, width, 0f).y) + 8f;
            }
            else if (line.Contains(" | "))
            {
                int bar = line.IndexOf(" | ", System.StringComparison.Ordinal);
                TMP_Text k = GuideCell(content, line.Substring(0, bar).Trim(), guideFontSize, FontStyles.Bold, textColor, 10f, keyWidth, y);
                TMP_Text a = GuideCell(content, line.Substring(bar + 3).Trim(), guideFontSize, FontStyles.Normal, textColor, 10f + keyWidth + gap, actionWidth, y);
                float hk = Mathf.Ceil(k.GetPreferredValues(k.text, keyWidth, 0f).y);
                float ha = Mathf.Ceil(a.GetPreferredValues(a.text, actionWidth, 0f).y);
                y += Mathf.Max(hk, ha) + 10f;
            }
            else if (trimmed.Length == 0)
            {
                if (any) y += guideFontSize * 0.6f;
                continue;
            }
            else
            {
                TMP_Text p = GuideCell(content, line, guideFontSize, FontStyles.Normal, textColor, 10f, width, y);
                y += Mathf.Ceil(p.GetPreferredValues(p.text, width, 0f).y) + 8f;
            }
            any = true;
        }

        if (!any)
        {
            TMP_Text e = GuideCell(content, s.empty, guideFontSize, FontStyles.Normal, textColor, 10f, width, 0f);
            y = Mathf.Ceil(e.GetPreferredValues(e.text, width, 0f).y);
        }
        s.scroll.content.anchoredPosition = Vector2.zero;
        PixelUIKit.UpdateScrollView(s.scroll, s.bar, y + 10f, s.viewHeight);
    }

    /// <summary>One wrapping text cell of the Controls / How to Play screens, top-left anchored at (x, -y).</summary>
    private TMP_Text GuideCell(RectTransform parent, string text, float size, FontStyles style, Color color, float x, float width, float y)
    {
        TMP_Text t = MakeText(parent, "Guide Text", text, size, style);
        t.alignment = TextAlignmentOptions.TopLeft;
        t.richText = true;
        t.color = color;
        RectTransform r = t.rectTransform;
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
        r.sizeDelta = new Vector2(width, 10f);
        r.anchoredPosition = new Vector2(x, -y);
        return t;
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
        settingsPanel = BuildSectionPanel("Settings Panel", settingsTitle, out float top);
        settingsRefreshers.Clear();

        // Everything scrolls: there are more rows than fit between the black bars.
        float viewHeight = FitViewHeight(settingsViewHeight, top);
        ScrollRect scroll = PixelUIKit.CreateScrollView(settingsPanel.transform, "Settings List", scrollbarColor, 12f, rowHeight,
                                                        out RectTransform content, out GameObject bar);
        RectTransform vr = scroll.GetComponent<RectTransform>();
        vr.anchorMin = new Vector2(0f, 1f);
        vr.anchorMax = new Vector2(1f, 1f);
        vr.pivot = new Vector2(0.5f, 1f);
        vr.sizeDelta = new Vector2(0f, viewHeight);
        vr.anchoredPosition = new Vector2(0f, -top);

        Transform list = content;
        float y = 0f;

        // Replay tutorial sits at the very top.
        AddWideButtonRow(list, replayTutorialText, () =>
        {
            PixelHints.ReplayTutorial();
            SetPaused(false); // so the tips show over the game
        }, ref y);

        // ---- Gameplay ----
        AddHeaderRow(list, headerGameplay, ref y);
        rotationToggle = AddToggleRow(list, rotationLabel, clicker == null || clicker.AllowRotation,
                                      on => { if (clicker != null) clicker.AllowRotation = on; }, ref y);
        pulsingToggle = AddToggleRow(list, pulsingLabel, clicker == null || clicker.AllowPulsing,
                                     on => { if (clicker != null) clicker.AllowPulsing = on; }, ref y);
        abbreviateToggle = AddToggleRow(list, abbreviateLabel.Replace(" (1.2K)", ""), PixelClicker.AbbreviateNumbers,
                                        on => PixelClicker.AbbreviateNumbers = on, ref y);
        hidePurchasedToggle = AddToggleRow(list, hidePurchasedLabel, PixelShop.HidePurchased,
                                           on => PixelShop.HidePurchased = on, ref y);
        backgroundToggle = AddToggleRow(list, runInBackgroundLabel, clicker != null && clicker.RunInBackground,
                                        on => { if (clicker != null) clicker.RunInBackground = on; }, ref y);
        pauseStopsToggle = AddToggleRow(list, pauseStopsLabel, pauseStopsGame, on =>
        {
            pauseStopsGame = on;
            PlayerPrefs.SetInt(PrefPauseStops, on ? 1 : 0);
            ApplyFreeze(); // takes effect right away, even though the menu is open
        }, ref y);

        // ---- Interface ----
        AddHeaderRow(list, headerInterface, ref y);
        AddSettingToggle(list, tipsLabel, () => PixelHints.TipsEnabled, on => PixelHints.TipsEnabled = on, ref y);
        AddSettingToggle(list, eventLogLabel, () => PixelHints.EventLogEnabled, on => PixelHints.EventLogEnabled = on, ref y);
        AddSettingToggle(list, popupsLabel, () => PixelUI.PopupsEnabled, on => PixelUI.PopupsEnabled = on, ref y);
        AddSettingToggle(list, cameraIntroLabel, () => PixelCameraIntro.Enabled, on => PixelCameraIntro.Enabled = on, ref y);
        AddSettingToggle(list, colorBlindLabel, () => PixelDisplaySettings.ColorBlind, on => PixelDisplaySettings.ColorBlind = on, ref y);
        AddChoiceRow(list, eventLogSizeLabel, PixelHints.LogSizeNames, () => PixelHints.LogSizeChoice, v => PixelHints.LogSizeChoice = v, ref y);
        AddChoiceRow(list, popupSizeLabel, PixelUI.PopupSizeNames, () => PixelUI.PopupSizeChoice, v => PixelUI.PopupSizeChoice = v, ref y);
        string[] scaleNames = new string[PixelDisplaySettings.UIScaleChoices.Length];
        for (int i = 0; i < scaleNames.Length; i++) scaleNames[i] = Mathf.RoundToInt(PixelDisplaySettings.UIScaleChoices[i] * 100f) + "%";
        AddChoiceRow(list, uiScaleLabel, scaleNames, NearestUiScaleIndex, v => PixelDisplaySettings.SavedUIScale = PixelDisplaySettings.UIScaleChoices[v], ref y);

        // ---- Saving ----
        if (saveGame != null)
        {
            AddHeaderRow(list, headerSaving, ref y);
            AddSettingToggle(list, autoSaveMessageLabel, () => saveGame.AutoSaveMessageNow,
                             on => PixelSaveGame.AutoSaveMessageChoice = on ? 1 : 0, ref y);
            AddChoiceRow(list, autoSaveLabel, PixelSaveGame.AutoSaveNames, CurrentAutoSaveIndex, v => PixelSaveGame.AutoSaveChoice = v, ref y);
        }

        // ---- Display ----
        AddHeaderRow(list, headerDisplay, ref y);
        AddLookRow(list, floorStyleLabel, () => PixelFloor.Instance, ref y);
        AddLookRow(list, skyStyleLabel, () => PixelSkybox.Instance, ref y);
        AddSettingToggle(list, horizonFogLabel, () => PixelHorizonFog.Enabled, on => PixelHorizonFog.Enabled = on, ref y);
        AddSettingToggle(list, fancyButtonsLabel, () => PixelUIKit.FancyButtons, on => PixelUIKit.FancyButtons = on, ref y);
        AddSettingToggle(list, fancyWindowsLabel, () => PixelUIKit.FancyWindows, on => PixelUIKit.FancyWindows = on, ref y);
        AddSettingToggle(list, barGlowLabel, () => PixelHud.BarGlow, on => PixelHud.BarGlow = on, ref y);
        AddSliderRow(list, glowHueLabel, PixelHud.GlowHue, v => PixelHud.GlowHue = v, ref y);
        AddSettingToggle(list, glowCycleLabel, () => PixelHud.GlowCycle, on => PixelHud.GlowCycle = on, ref y);
        AddSettingToggle(list, uiColorLabel, () => PixelUIKit.UiColorOn, on => PixelUIKit.UiColorOn = on, ref y);
        AddSliderRow(list, uiHueLabel, PixelUIKit.UiHue, v => PixelUIKit.UiHue = v, ref y);
        AddSettingToggle(list, vsyncLabel, () => PixelDisplaySettings.VSync, on => PixelDisplaySettings.VSync = on, ref y);
        AddSettingToggle(list, fullscreenLabel, () => PixelDisplaySettings.Fullscreen, on => PixelDisplaySettings.Fullscreen = on, ref y);
        List<Vector2Int> resolutions = PixelDisplaySettings.Resolutions;
        string[] resolutionNames = new string[resolutions.Count];
        for (int i = 0; i < resolutionNames.Length; i++) resolutionNames[i] = resolutions[i].x + " x " + resolutions[i].y;
        AddChoiceRow(list, resolutionLabel, resolutionNames, () => Mathf.Max(0, resolutions.IndexOf(PixelDisplaySettings.Resolution)),
                     v => PixelDisplaySettings.Resolution = resolutions[v], ref y);

        // ---- Audio: the mute box, then three volume sliders (the sound system is added by Start, which runs first) ----
        PixelAudio audio = PixelFind.First<PixelAudio>();
        if (audio != null)
        {
            AddHeaderRow(list, headerAudio, ref y);
            muteToggle = AddToggleRow(list, muteLabel, audio.Muted, on => audio.Muted = on, ref y);
            masterSlider = AddSliderRow(list, masterVolumeLabel, audio.MasterVolume, v => audio.MasterVolume = v, ref y);
            effectsSlider = AddSliderRow(list, effectsVolumeLabel, audio.EffectsVolume, v => audio.EffectsVolume = v, ref y);
            musicSlider = AddSliderRow(list, musicVolumeLabel, audio.MusicVolume, v => audio.MusicVolume = v, ref y);
        }

        // ---- Controls ----
        AddHeaderRow(list, headerControls, ref y);
        AddWideButtonRow(list, keyBindingsText, () => ShowView(keysPanel), ref y);

        PixelUIKit.UpdateScrollView(scroll, bar, y, viewHeight);
        BuildKeysPanel();
        FinishSectionPanel(settingsPanel, top + viewHeight);
    }

    private int NearestUiScaleIndex()
    {
        float saved = PixelDisplaySettings.SavedUIScale;
        int best = 0;
        for (int i = 1; i < PixelDisplaySettings.UIScaleChoices.Length; i++)
            if (Mathf.Abs(PixelDisplaySettings.UIScaleChoices[i] - saved) < Mathf.Abs(PixelDisplaySettings.UIScaleChoices[best] - saved)) best = i;
        return best;
    }

    private int CurrentAutoSaveIndex()
    {
        if (PixelSaveGame.AutoSaveChoice >= 0) return PixelSaveGame.AutoSaveChoice;
        // Not chosen yet: show the choice closest to the Inspector's interval.
        float inspector = saveGame != null ? saveGame.InspectorAutoSaveSeconds : 0f;
        int best = 0;
        for (int i = 1; i < PixelSaveGame.AutoSaveChoices.Length; i++)
            if (Mathf.Abs(PixelSaveGame.AutoSaveChoices[i] - inspector) < Mathf.Abs(PixelSaveGame.AutoSaveChoices[best] - inspector)) best = i;
        return best;
    }

    /// <summary>A coloured section title inside the settings list.</summary>
    private void AddHeaderRow(Transform parent, string text, ref float y)
    {
        y += 8f;
        TMP_Text t = MakeText(parent, text + " Header", text, rowFontSize * 0.9f, FontStyles.Bold);
        t.color = statValueColor;
        t.alignment = TextAlignmentOptions.MidlineLeft;
        RectTransform rt = t.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(-80f, rowHeight * 0.8f);
        rt.anchoredPosition = new Vector2(0f, -y);
        y += rowHeight * 0.8f + 4f;
    }

    /// <summary>A tick box that reads / writes a setting; it re-reads the setting whenever the screen is shown.</summary>
    private void AddSettingToggle(Transform parent, string label, System.Func<bool> get, System.Action<bool> set, ref float y)
    {
        Toggle toggle = AddToggleRow(parent, label, get(), on => set(on), ref y);
        settingsRefreshers.Add(() => toggle.SetIsOnWithoutNotify(get()));
    }

    /// <summary>A label with a button on the right that steps through the options each click (wraps around).</summary>
    private void AddChoiceRow(Transform parent, string label, string[] options, System.Func<int> get, System.Action<int> set, ref float y)
    {
        TMP_Text choiceLabel = AddRowLabel(parent, label, y, out RectTransform row);
        choiceLabel.rectTransform.anchorMax = new Vector2(0.6f, 1f);
        choiceLabel.rectTransform.offsetMax = new Vector2(-14f, 0f);   // a gap before the button
        Button b = MakeButton(row, "Choice", "", new Vector2(10f, 10f), tickBoxColor, rowFontSize * 0.8f);
        RectTransform br = b.GetComponent<RectTransform>();
        br.anchorMin = new Vector2(0.62f, 0.06f);
        br.anchorMax = new Vector2(1f, 0.94f);
        br.offsetMin = br.offsetMax = Vector2.zero;
        TMP_Text text = b.GetComponentInChildren<TMP_Text>();
        text.enableAutoSizing = true;
        text.fontSizeMax = rowFontSize * 0.8f;
        text.fontSizeMin = 12f;
        text.overflowMode = TextOverflowModes.Ellipsis;

        System.Action refresh = () =>
        {
            int index = options.Length == 0 ? 0 : Mathf.Clamp(get(), 0, options.Length - 1);
            text.text = options.Length == 0 ? "-" : options[index];
        };
        b.onClick.AddListener(() =>
        {
            if (options.Length == 0) return;
            set((Mathf.Clamp(get(), 0, options.Length - 1) + 1) % options.Length);
            refresh();
        });
        refresh();
        settingsRefreshers.Add(refresh);
        y += rowHeight + 6f;
    }

    /// <summary>A full-width button row (an action rather than a setting).</summary>
    private void AddWideButtonRow(Transform parent, string label, UnityEngine.Events.UnityAction onClick, ref float y)
    {
        Button b = MakeButton(parent, label + " Button", label, new Vector2(10f, rowHeight), menuButtonColor, rowFontSize * 0.9f);
        RectTransform rt = b.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(-80f, rowHeight);
        rt.anchoredPosition = new Vector2(0f, -y);
        b.onClick.AddListener(onClick);
        y += rowHeight + 6f;
    }

    // ------------------------------------------------------------------
    // Key bindings
    // ------------------------------------------------------------------

    private void BuildKeysPanel()
    {
        keysPanel = BuildSectionPanel("Key Bindings Panel", keyBindingsTitle, out float y);
        int count = PixelKeys.Count;
        keyButtonLabels = new TMP_Text[count];

        for (int i = 0; i < count; i++)
        {
            int index = i;
            string label = keyActionLabels != null && i < keyActionLabels.Length ? keyActionLabels[i] : (PixelAction)i == PixelAction.TimeSlow ? "Time slow" : (PixelAction)i == PixelAction.FirstPerson ? "First person mode" : ((PixelAction)i).ToString();
            TMP_Text keyLabel = AddRowLabel(keysPanel.transform, label, y, out RectTransform row);
            keyLabel.enableAutoSizing = true;           // long names ("Turn left (fan / sorter)") shrink to fit, they are not cut off
            keyLabel.fontSizeMax = rowFontSize * 0.65f;
            keyLabel.fontSizeMin = 9f;
            keyLabel.rectTransform.anchorMax = new Vector2(0.66f, 1f);
            keyLabel.rectTransform.offsetMax = new Vector2(-12f, 0f);
            Button b = MakeButton(row, "Key", "", new Vector2(10f, 10f), tickBoxColor, rowFontSize * 0.85f);
            RectTransform br = b.GetComponent<RectTransform>();
            br.anchorMin = new Vector2(0.68f, 0.06f);
            br.anchorMax = new Vector2(1f, 0.94f);
            br.offsetMin = br.offsetMax = Vector2.zero;
            TMP_Text text = b.GetComponentInChildren<TMP_Text>();
            text.enableAutoSizing = true;
            text.fontSizeMax = rowFontSize * 0.85f;
            text.fontSizeMin = 12f;
            keyButtonLabels[i] = text;
            b.onClick.AddListener(() => { capturingAction = index; RefreshKeyLabels(); });
            y += rowHeight + 6f;
        }

        y += 18f;
        AddMenuButton(keysPanel.transform, resetKeysText, menuButtonColor, ref y, () =>
        {
            PixelKeys.ResetAll();
            capturingAction = -1;
            RefreshKeyLabels();
        });
        Transform resetButton = keysPanel.transform.GetChild(keysPanel.transform.childCount - 1);
        TMP_Text resetText = resetButton.GetComponentInChildren<TMP_Text>();
        if (resetText != null)
        {
            resetText.enableAutoSizing = true;          // one line
            resetText.fontSizeMax = menuButtonFontSize;
            resetText.fontSizeMin = 12f;
#if UNITY_2023_1_OR_NEWER
            resetText.textWrappingMode = TextWrappingModes.NoWrap;
#else
            resetText.enableWordWrapping = false;
#endif
        }
        RefreshKeyLabels();
        FinishSectionPanel(keysPanel, y, settingsPanel);
    }

    private void RefreshKeyLabels()
    {
        if (keyButtonLabels == null) return;
        for (int i = 0; i < keyButtonLabels.Length; i++)
            keyButtonLabels[i].text = capturingAction == i ? pressAKeyText : PixelKeys.Name((PixelAction)i);
    }

    /// <summary>While a key button waits for a key: the next key press becomes the binding (Escape cancels).</summary>
    private bool UpdateKeyCapture()
    {
        if (capturingAction < 0) return false;
        if (EscapePressed())
        {
            capturingAction = -1;
            RefreshKeyLabels();
            return true;
        }
        if (PixelKeys.TryCapture(out KeyCode key))
        {
            PixelKeys.Set((PixelAction)capturingAction, key);
            capturingAction = -1;
            RefreshKeyLabels();
        }
        return true; // nothing else (like Escape closing the menu) reacts while waiting
    }

    /// <summary>
    /// A "pick a look" row (floor style, sky): a picture of the current look, its name, and arrows to step through
    /// the looks. 'source' is asked again each time, so it works even if the look component wakes up later.
    /// </summary>
    private void AddLookRow(Transform parent, string labelText, System.Func<IPixelLookSource> source, ref float y)
    {
        TMP_Text label = AddRowLabel(parent, labelText, y, out RectTransform row);
        label.rectTransform.anchorMax = new Vector2(0.4f, 1f);

        // One drop-down button: picture + name + a down arrow. The list opens over everything (see OpenLookList).
        Button pick = MakeButton(row, labelText + " Picker", "", new Vector2(10f, 10f), tickBoxColor, rowFontSize * 0.8f);
        RectTransform pr = pick.GetComponent<RectTransform>();
        pr.anchorMin = new Vector2(0.42f, 0.06f);
        pr.anchorMax = new Vector2(1f, 0.94f);
        pr.offsetMin = pr.offsetMax = Vector2.zero;

        float icon = rowHeight * 0.7f;
        GameObject swatchGo = new GameObject("Preview", typeof(RectTransform), typeof(RawImage));
        swatchGo.transform.SetParent(pick.transform, false);
        RawImage swatch = swatchGo.GetComponent<RawImage>();
        swatch.raycastTarget = false;
        RectTransform sr = swatchGo.GetComponent<RectTransform>();
        sr.anchorMin = sr.anchorMax = sr.pivot = new Vector2(0f, 0.5f);
        sr.sizeDelta = new Vector2(icon, icon);
        sr.anchoredPosition = new Vector2(8f, 0f);

        TMP_Text nameText = pick.GetComponentInChildren<TMP_Text>();
        nameText.color = statValueColor;
        nameText.alignment = TextAlignmentOptions.MidlineLeft;
        nameText.enableAutoSizing = true;
        nameText.fontSizeMax = rowFontSize * 0.7f;
        nameText.fontSizeMin = 12f;
        nameText.overflowMode = TextOverflowModes.Ellipsis;
        RectTransform nr = nameText.rectTransform;
        nr.anchorMin = Vector2.zero;
        nr.anchorMax = Vector2.one;
        nr.offsetMin = new Vector2(icon + 18f, 0f);
        nr.offsetMax = new Vector2(-icon - 6f, 0f);

        Button arrow = MakeButton(pick.transform, "Arrow", ">", new Vector2(icon * 0.7f, icon * 0.7f), floorArrowColor, rowFontSize);
        RectTransform ar = arrow.GetComponent<RectTransform>();
        ar.anchorMin = ar.anchorMax = ar.pivot = new Vector2(1f, 0.5f);
        ar.anchoredPosition = new Vector2(-8f, 0f);
        PixelUIKit.UseArrowGlyph(arrow, false);
        ar.localRotation = Quaternion.Euler(0f, 0f, -90f);   // points down

        System.Action refresh = () =>
        {
            CloseLookList();
            IPixelLookSource look = source();
            bool usable = look != null && look.Usable;
            nameText.text = usable ? look.StyleName(look.Current) : noFloorText;
            swatch.texture = usable ? look.PreviewTexture(look.Current) : null;
            swatch.color = usable ? look.PreviewColor(look.Current) : new Color(1f, 1f, 1f, 0.15f);
            swatch.uvRect = usable ? look.PreviewRect : new Rect(0f, 0f, 1f, 1f);
        };
        System.Action open = () =>
        {
            IPixelLookSource look = source();
            if (look == null || !look.Usable) return;
            OpenLookList(pr, look, refresh);
        };
        pick.onClick.AddListener(() => open());
        arrow.onClick.AddListener(() => open());

        settingsRefreshers.Add(refresh);
        y += rowHeight + 6f;
    }

    private GameObject lookListRoot;

    private void CloseLookList()
    {
        if (lookListRoot != null) Destroy(lookListRoot);
        lookListRoot = null;
    }

    /// <summary>The look drop-down: a list of every look (picture + name) that opens under (or above) the button, over the whole menu.</summary>
    private void OpenLookList(RectTransform anchor, IPixelLookSource look, System.Action refresh)
    {
        CloseLookList();
        RectTransform canvasRect = canvasRoot.GetComponent<RectTransform>();

        // Full-screen catcher: a click anywhere else closes the list.
        lookListRoot = new GameObject("Look List", typeof(RectTransform), typeof(Image), typeof(Button));
        lookListRoot.transform.SetParent(canvasRoot.transform, false);
        lookListRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
        PixelUIKit.Stretch(lookListRoot.GetComponent<RectTransform>());
        lookListRoot.GetComponent<Button>().onClick.AddListener(CloseLookList);

        Vector3[] corners = new Vector3[4];
        anchor.GetWorldCorners(corners);
        Vector2 bl = canvasRect.InverseTransformPoint(corners[0]);
        Vector2 tr = canvasRect.InverseTransformPoint(corners[2]);
        float width = tr.x - bl.x;
        float itemH = rowHeight * 0.85f;
        int count = look.StyleCount;
        float height = Mathf.Min(count, 6) * itemH + 12f;
        float below = bl.y - (-canvasRect.rect.height * 0.5f);   // room under the button
        bool goUp = below < height + 20f;

        GameObject boxGo = new GameObject("Box", typeof(RectTransform), typeof(Image));
        boxGo.transform.SetParent(lookListRoot.transform, false);
        Image boxImage = boxGo.GetComponent<Image>();
        boxImage.color = tickBoxColor;
        PixelUIKit.StyleBox(boxImage, true);
        RectTransform box = boxGo.GetComponent<RectTransform>();
        box.anchorMin = box.anchorMax = new Vector2(0.5f, 0.5f);
        box.pivot = new Vector2(0f, goUp ? 0f : 1f);
        box.sizeDelta = new Vector2(width, height);
        box.anchoredPosition = goUp ? new Vector2(bl.x, tr.y + 4f) : new Vector2(bl.x, bl.y - 4f);

        ScrollRect scroll = PixelUIKit.CreateScrollView(boxGo.transform, "List", scrollbarColor, 10f, itemH,
                                                        out RectTransform content, out GameObject bar);
        RectTransform vr = scroll.GetComponent<RectTransform>();
        vr.anchorMin = Vector2.zero;
        vr.anchorMax = Vector2.one;
        vr.offsetMin = new Vector2(6f, 6f);
        vr.offsetMax = new Vector2(-6f, -6f);

        float icon = itemH * 0.75f;
        for (int i = 0; i < count; i++)
        {
            int index = i;
            Button item = MakeButton(content, "Look " + i, look.StyleName(i), new Vector2(10f, itemH - 4f),
                                     i == look.Current ? floorArrowColor : tickBoxColor, rowFontSize * 0.7f);
            RectTransform ir = item.GetComponent<RectTransform>();
            ir.anchorMin = new Vector2(0f, 1f);
            ir.anchorMax = new Vector2(1f, 1f);
            ir.pivot = new Vector2(0.5f, 1f);
            ir.sizeDelta = new Vector2(-14f, itemH - 4f);
            ir.anchoredPosition = new Vector2(0f, -i * itemH);

            TMP_Text t = item.GetComponentInChildren<TMP_Text>();
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.enableAutoSizing = true;
            t.fontSizeMax = rowFontSize * 0.7f;
            t.fontSizeMin = 12f;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.rectTransform.offsetMin = new Vector2(icon + 18f, 0f);
            t.rectTransform.offsetMax = new Vector2(-6f, 0f);

            GameObject pic = new GameObject("Preview", typeof(RectTransform), typeof(RawImage));
            pic.transform.SetParent(item.transform, false);
            RawImage ri = pic.GetComponent<RawImage>();
            ri.raycastTarget = false;
            ri.texture = look.PreviewTexture(i);
            ri.color = look.PreviewColor(i);
            ri.uvRect = look.PreviewRect;
            RectTransform pr2 = pic.GetComponent<RectTransform>();
            pr2.anchorMin = pr2.anchorMax = pr2.pivot = new Vector2(0f, 0.5f);
            pr2.sizeDelta = new Vector2(icon, icon);
            pr2.anchoredPosition = new Vector2(8f, 0f);

            item.onClick.AddListener(() =>
            {
                look.SetStyle(index);
                CloseLookList();
                refresh();
            });
        }
        PixelUIKit.UpdateScrollView(scroll, bar, count * itemH, height - 12f);
        // Bring the current look into view.
        float visible = height - 12f;
        float want = Mathf.Max(0f, look.Current * itemH - visible * 0.5f + itemH * 0.5f);
        content.anchoredPosition = new Vector2(0f, Mathf.Min(want, Mathf.Max(0f, count * itemH - visible)));
    }

    private string FormatCount(double value)
    {
        if (!PixelClicker.AbbreviateNumbers || value < 1000d) return System.Math.Floor(value).ToString("N0");
        return PixelClicker.FormatNumber(value);
    }

    private void RefreshStats()
    {
        if (stats == null || statsContent == null) return;
        List<PixelStats.Line> lines = stats.GetLines(FormatCount);

        // Rows are rebuilt only when the list of rows itself changed (a new pixel type, a minigame that started...).
        string signature = lines.Count.ToString();
        foreach (PixelStats.Line l in lines) signature += "|" + (l.header ? "#" : "") + l.label;
        if (signature != statsSignature)
        {
            statsSignature = signature;
            RebuildStatRows(lines);
        }
        else
        {
            int v = 0;
            bool hidden = false;
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].header) { hidden = collapsedStatSections.Contains(lines[i].label); continue; }
                if (hidden) continue;
                if (v < statValueTexts.Count) statValueTexts[v++].text = lines[i].value;
            }
        }
        PixelUIKit.UpdateScrollView(statsScroll, statsBar, statsContentHeight, statsViewHeight);
    }

    private readonly HashSet<string> collapsedStatSections = new HashSet<string>();

    /// <summary>A Stats section heading you can click to fold / unfold the rows under it ("-" open, "+" folded).</summary>
    private void AddStatSectionHeader(Transform parent, string text, bool collapsed, ref float y)
    {
        y += 8f;
        float h = rowHeight * 0.8f;
        Button b = MakeButton(parent, text + " Header", (collapsed ? "+  " : "-  ") + text, new Vector2(10f, h), tickBoxColor, rowFontSize * 0.7f);
        RectTransform rt = b.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(-80f, h);
        rt.anchoredPosition = new Vector2(0f, -y);
        TMP_Text t = b.GetComponentInChildren<TMP_Text>();
        t.color = statValueColor;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.MidlineLeft;
        t.rectTransform.offsetMin = new Vector2(16f, 0f);
        b.onClick.AddListener(() =>
        {
            if (!collapsedStatSections.Remove(text)) collapsedStatSections.Add(text);
            RebuildStatRows();
            PixelUIKit.UpdateScrollView(statsScroll, statsBar, statsContentHeight, statsViewHeight);
        });
        y += h + 4f;
    }

    private void RebuildStatRows(List<PixelStats.Line> lines = null)
    {
        if (statsContent == null) return;
        for (int i = statsContent.childCount - 1; i >= 0; i--) Destroy(statsContent.GetChild(i).gameObject);
        statValueTexts.Clear();
        if (lines == null && stats != null) lines = stats.GetLines(FormatCount);

        float cy = 0f;
        if (lines != null)
        {
            bool hidden = false;
            foreach (PixelStats.Line line in lines)
            {
                if (line.header)
                {
                    hidden = collapsedStatSections.Contains(line.label);
                    AddStatSectionHeader(statsContent, line.label, hidden, ref cy);
                }
                else if (!hidden)
                {
                    TMP_Text value = AddStatRow(statsContent, line.label, ref cy);
                    value.text = line.value;
                    statValueTexts.Add(value);
                }
            }
        }
        statsContentHeight = cy;
    }

    /// <summary>Two half-width buttons side by side on one row (keeps the menu short).</summary>
    /// <summary>
    /// Lays the main menu's buttons out in columns and sizes the panel to fit them: wider than tall, and (if Fit Between
    /// Bars is on) never taller than the space between the black bars - the buttons shrink if there are too many.
    /// </summary>
    private void LayoutMainButtons(Transform parent, List<MenuEntry> entries, float top, RectTransform panelRect)
    {
        int columns = Mathf.Max(1, mainColumns);
        float cellWidth = (mainPanelWidth - 80f - columnGap * (columns - 1)) / columns;

        // Work out each button's row and column first. A full-row entry starts a new row and takes it alone;
        // a lone last button also spans its row.
        int[] rowOf = new int[entries.Count], colOf = new int[entries.Count];
        bool[] spans = new bool[entries.Count];
        int rowNow = 0, colNow = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].fullRow || columns == 1)
            {
                if (colNow != 0) { rowNow++; colNow = 0; }
                rowOf[i] = rowNow; colOf[i] = 0; spans[i] = true;
                rowNow++;
                continue;
            }
            rowOf[i] = rowNow; colOf[i] = colNow;
            colNow++;
            if (colNow >= columns) { colNow = 0; rowNow++; }
        }
        if (colNow != 0)
        {
            // The last row has a single button: let it span the row.
            int last = entries.Count - 1;
            if (!spans[last] && colOf[last] == 0) spans[last] = true;
            rowNow++;
        }
        int rows = rowNow;

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
            float width = spans[i] ? mainPanelWidth - 80f : cellWidth;
            float x = spans[i] ? 0f : -(mainPanelWidth - 80f) * 0.5f + cellWidth * 0.5f + colOf[i] * (cellWidth + columnGap);

            Button button = MakeButton(parent, entries[i].label + " Button", entries[i].label, new Vector2(width, buttonHeight),
                                       entries[i].color, menuButtonFontSize);
            RectTransform rt = button.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(x, -(top + rowOf[i] * (buttonHeight + menuButtonSpacing)));
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
        return Mathf.Max(300f, referenceResolution.y / PixelDisplaySettings.UIScale - 2f * (bar + barMargin));
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
