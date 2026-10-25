// ---- System notes (moved here from CLAUDE.md) ----
// PixelPauseMenu.cs
//   Pause menu (main menu = mainColumns-column button grid LayoutMainButtons, mainPanelWidth wide; fitBetweenBars keeps it and the
//   text screens between the black bars via AvailableHeight/FitViewHeight) (Esc; the on-screen Pause button is off by default -
//   showPauseButtonOnScreen): sets Time.timeScale = 0, Resume (alone) / Save-Load + Stats / Settings + Game Guide (Controls, How To
//   Play, Database tabs) / Give feedback (alone) / Quit (alone; opens a 'Quit to...' box with Desktop and Main menu - Main menu saves,
//   then PixelTitleScreen.ReturnToMenu()) (MenuEntry.fullRow) (Restart now lives on the Stats screen, Report Folder moved to the Dev
//   Tools panel; the Stats screen shows one total "Potions used" row instead of the potions drop-down; a search box over the Stats list filters rows by name / value and shows a section when its heading or any row matches, StatRowsShown) (Settings: tick boxes for cube
//   rotation, pulsing, "Abbreviate numbers" and "Hide purchased shop items" (static PixelShop.HidePurchased, PlayerPrefs;
//   HiddenAsPurchased in the shop list keeps a bought pack listed while it still has upgrades to buy) ->
//   PixelClicker.AllowRotation/AllowPulsing and static PixelClicker.AbbreviateNumbers (read by FormatNumber, PixelUI, PixelLog and the
//   stats), stored in PlayerPrefs), PixelPauseMenu.IsPaused. PixelClicker ignores clicks while Time.timeScale <= 0.
//   Part files (partial class): PixelPauseMenuRows (screen + row builders), PixelPauseMenuStats, PixelPauseMenuSaveLoad (save slots, Quit,
//   Restart), PixelPauseMenuGuide, PixelPauseMenuSettings (menu search + Settings list), PixelPauseMenuLooks (look rows + floor pop-out),
//   PixelPauseMenuKeysPlaylist (key bindings + custom playlist).
// ---------------------------------------------------
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
public partial class PixelPauseMenu : MonoBehaviour
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

    [Header("Music player")]
    [Tooltip("Show the music player (track list, arrows, mute, shuffle) at the bottom centre of the pause menu, like on the main menu.")]
    [SerializeField] private bool showMusicPlayer = true;
    [Min(0f)]
    [Tooltip("Seconds the pause menu's music player stays after the menu opens or the mouse leaves it. 0 = the default (1.5).")]
    [SerializeField] private float pauseMusicHideSeconds = 0f;
    [Min(0f)]
    [Tooltip("Seconds the pause menu's music player takes to fade out. 0 = the default (0.15; the main menu's takes 0.8).")]
    [SerializeField] private float pauseMusicFadeSeconds = 0f;

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

    [Tooltip("Seconds the red Overwrite button must be held down to overwrite an existing save.")]
    [SerializeField] private float overwriteHoldSeconds = 0.6f;

    [Tooltip("Text on the Overwrite button (it is a hold button). Empty = the normal confirm text.")]
    [SerializeField] private string overwriteHoldText = "Hold to overwrite";

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
    [SerializeField] private string saveLoadMenuText = "Save/Load";

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

    [Tooltip("Colour of the overwrite warning title (the Overwrite button itself is a Danger button, see PixelUITheme).")]
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


    [Tooltip("Result text when loading failed.")]
    [SerializeField] private string loadFailedResultText = "Load failed";

    [Tooltip("Save button text.")]
    [SerializeField] private string saveText = "Save";

    [Tooltip("Load button text.")]
    [SerializeField] private string loadText = "Load";

    [Tooltip("The Load button's text on the main menu's slot window while an EMPTY slot is selected: pressing it starts a new game in that slot.")]
    [SerializeField] private string newGameButtonText = "New Game";

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


    [Tooltip("Title of the Stats section.")]
    [SerializeField] private string statsTitle = "Stats";
    [Tooltip("Placeholder in the Stats screen's search box (filters the rows by name or value as you type).")]
    [SerializeField] private string statsSearchPlaceholder = "Search stats...";
    [Tooltip("Shown in the Stats list when the search matches nothing.")]
    [SerializeField] private string statsNoMatchText = "No stats match your search.";

    [Tooltip("Title of the Settings section.")]
    [SerializeField] private string settingsTitle = "Settings";

    [Min(200f)]
    [Tooltip("Height of the scrolling Settings list (canvas units). It is shortened to fit between the black bars.")]
    [SerializeField] private float settingsViewHeight = 640f;

    [Header("Settings Screen Rows")]
    [Tooltip("Heading of the gameplay section in Settings.")]
    [SerializeField] private string headerGameplay = "Gameplay";
    [Tooltip("Heading of the interface section in Settings.")]
    [SerializeField] private string headerInterface = "Interface";

    [Tooltip("Heading of the accessibility section in Settings.")]
    [SerializeField] private string headerAccessibility = "Accessibility";
    [Tooltip("Heading of the display section in Settings.")]
    [SerializeField] private string headerDisplay = "Display";
    [Tooltip("Heading of the audio section in Settings.")]
    [SerializeField] private string headerAudio = "Audio";
    [Tooltip("Settings row: show the first-time tip boxes.")]
    [SerializeField] private string tipsLabel = "Show first-time tips";
    [Tooltip("Settings button that plays the opening tutorial again.")]
    [SerializeField] private string replayTutorialText = "Replay tutorial";

    [Tooltip("Text of the Settings button that opens the feedback window.")]
    [SerializeField] private string feedbackText = "Give feedback";
    [Tooltip("Settings row: text size of the event log.")]
    [SerializeField] private string eventLogSizeLabel = "Event log text size";
    [Tooltip("Settings row: show the +N number popups on clicks.")]
    [SerializeField] private string popupsLabel = "Number popups (+N)";
    [Tooltip("Settings row: show block tooltips (Alt key).")]
    [SerializeField] private string blockTipsLabel = "Block tooltips (Alt: tap = next, hold = peek)";
    [Tooltip("Settings row: size of the number popups.")]
    [SerializeField] private string popupSizeLabel = "Popup size";
    [Tooltip("Label of the Settings choice for how the docked menu buttons behave.")]
    [SerializeField] private string buttonModeLabel = "Menu buttons";

    [Tooltip("Settings row: UI scale (applied after a restart).")]
    [SerializeField] private string uiScaleLabel = "UI scale (after restart)";
    [Tooltip("Settings row: colour-blind marks on blocks.")]
    [SerializeField] private string colorBlindLabel = "Colour-blind marks";
    [Tooltip("Label of the small button in the Save / Load window that opens the list of autosaves.")]
    [SerializeField] private string autosavesText = "Autosaves";
    [Tooltip("Label of the Settings button that puts the achievement popup back to its default spot (the middle, above the combo meter).")]
    [SerializeField] private string resetPopupPinText = "Reset achievement popup position";
    [Tooltip("Settings > Accessibility (under the achievement popup reset): the button that puts the Did you know tips box back at its default spot (docked at the right edge, near the top).")]
    [SerializeField] private string resetTipsLocationText = "Reset tips location";
    [Tooltip("Label of the Settings tick box for the drop guide: a line and ring that show where a held husk would land.")]
    [SerializeField] private string dropGuideLabel = "Drop guide for held pixels";
    [Tooltip("Label of the custom UI button colour tick box in Settings (the eyedropper beside it picks the colour).")]
    [SerializeField] private string buttonColorLabel = "Custom UI button colour";
    [Tooltip("Label of the UI click sounds tick box (Settings > Audio).")]
    [SerializeField] private string uiClicksLabel = "UI click sounds";
    [Tooltip("Label of the fancy pause menu tick box in Settings (off = the classic menu).")]
    [SerializeField] private string fancyMenuLabel = "Fancy pause menu (animated)";
    [Tooltip("Label of the next-up window tick box in Settings.")]
    [SerializeField] private string nextUpLabel = "Next-up window (Block Hold)";
    [Tooltip("Label of the screen filter drop-down in Settings.")]
    [SerializeField] private string filterLabel = "Screen filter";
    [Tooltip("Label of the screen filter strength slider in Settings.")]
    [SerializeField] private string filterStrengthLabel = "Filter strength";
    [Tooltip("Label of the slider for the orbit camera's field of view (the default camera; Top down too).")]
    [SerializeField] private string orbitFovLabel = "Orbit field of view";
    [Tooltip("Settings row: play the camera intro at the next start.")]
    [SerializeField] private string cameraIntroLabel = "Camera intro (next start)";
    [Tooltip("Label of the switch for the Galactic Arcade studio intro at launch.")]
    [SerializeField] private string studioIntroLabel = "Studio intro (at launch)";
    [Tooltip("Settings row: day / night cycle on or off.")]
    [SerializeField] private string dayNightLabel = "Day / night cycle";
    [Tooltip("Settings > Gameplay: label of the 'Did you know?' tips switch.")]
    [SerializeField] private string didYouKnowLabel = "Did you know tips";
    [Tooltip("Settings row: how long one day lasts.")]
    [SerializeField] private string dayNightLengthLabel = "Length of a day";
    [Tooltip("Settings row: how often the game autosaves.")]
    [SerializeField] private string autoSaveLabel = "Autosave every";
    [Tooltip("Settings row: show a message when it autosaves.")]
    [SerializeField] private string autoSaveMessageLabel = "Show autosave message";
    [Tooltip("Settings row: VSync.")]
    [SerializeField] private string vsyncLabel = "VSync";
    [Tooltip("Settings row: fullscreen.")]
    [SerializeField] private string fullscreenLabel = "Fullscreen";
    [Tooltip("Settings row: screen resolution.")]
    [SerializeField] private string resolutionLabel = "Resolution";
    [Tooltip("Settings button that opens the key bindings screen.")]
    [SerializeField] private string keybindsButtonText = "Change keybinds";
    [Tooltip("Settings > Audio button that opens the song checklist, and that window's title.")]
    [SerializeField] private string customPlaylistText = "Custom playlist";
    [Tooltip("Title of the custom playlist screen.")]
    [SerializeField] private string playlistPanelTitle = "Custom playlist";
    [Tooltip("Playlist button that ticks every song.")]
    [SerializeField] private string playlistAllText = "Select all songs";
    [Tooltip("Hint under the custom playlist.")]
    [SerializeField] private string playlistHintText = "Only ticked songs play. At least one stays ticked.";
    [Tooltip("Title of the key bindings screen.")]
    [SerializeField] private string keyBindingsPanelTitle = "Keybinds";
    [Tooltip("Key bindings button that puts every key back to its default.")]
    [SerializeField] private string resetKeysText = "Reset keys to default";
    [Tooltip("Shown on a key binding while it waits for the new key.")]
    [SerializeField] private string pressAKeyText = "Press a key...";

    [Tooltip("Names of the rebindable actions, in the order of PixelAction (Time Stop, Hose, Turn left, Turn right, Event log, Time slow).")]
    [SerializeField] private string[] keyActionLabels = { "Time Stop", "Pixel Bank hose", "Turn left (placing)", "Turn right (placing)", "Event log", "Time slow" };

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
    [Tooltip("Label of the Settings choice that decides what the floor shows while 'Display floor' is off (invisible, a flat plane, or a pulse where pixels land).")]
    [SerializeField] private string floorOffModeLabel = "When floor is off";

    [Tooltip("Label of the sky picker (arrows step through the skies of the Pixel Skybox component).")]
    [SerializeField] private string skyStyleLabel = "Sky";

    [Tooltip("Label of the Settings tick box that switches on your own colour for the black bars (game and main menu).")]
    [SerializeField] private string barColorLabel = "Custom bar colour";

    [Tooltip("Label of the Settings tick box that switches on your own window colour.")]
    [SerializeField] private string uiColorLabel = "Custom UI colour";

    [Tooltip("Label of the Settings tick box for the neon glow along the black bars.")]
    [SerializeField] private string barGlowLabel = "Neon bar glow";
    [Tooltip("Label of the drop-down that picks what glows along the black bars (neon glow, pixel equalizer, wave, marquee, scanner, thin line).")]
    [SerializeField] private string barStyleLabel = "Bar style";

    [Tooltip("Label of the tick box for the soft glow behind the clicker cube.")]
    [SerializeField] private string clickerAuraLabel = "Clicker glow aura";

    [Tooltip("Label of the drop-down that chooses where the aura's colour comes from.")]
    [SerializeField] private string clickerAuraColorLabel = "Aura color";

    [Tooltip("Shown in the floor style picker when the scene has no floor to restyle.")]
    [SerializeField] private string noFloorText = "No floor found";

    [Tooltip("Label of the master volume slider.")]
    [SerializeField] private string masterVolumeLabel = "Master volume";

    [Tooltip("Label of the effects volume slider.")]
    [SerializeField] private string effectsVolumeLabel = "Effects volume";

    [Tooltip("Label of the music volume slider.")]
    [SerializeField] private string musicVolumeLabel = "Music volume";

    [Tooltip("Label of the mute tick box.")]
    [SerializeField] private string muteLabel = "Mute all sound";

    [Tooltip("Label of the music-only mute tick box (Settings > Audio; saved per save file).")]
    [SerializeField] private string muteMusicLabel = "Mute music";

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

    [Header("How to Play and Controls")]
    [Tooltip("Show the How To Play tab of the Game Guide screen.")]
    [SerializeField] private bool showHowToPlay = true;

    [Tooltip("Text of the How To Play tab (the written guide to the game).")]
    [SerializeField] private string howToPlayTabText = "How To Play";

    [Tooltip("Text of the Database tab, which opens the searchable game database.")]
    [SerializeField] private string databaseTabText = "Database";

    [Tooltip("Text of the pause menu's Game Guide button and the title of its screen (it has three tabs: Controls, How To Play and Database).")]
    [SerializeField] private string guideMenuText = "Game Guide";

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

    [Min(0f)]
    [Tooltip("Width of the Key Bindings screen (canvas units). 0 = the default (1150).")]
    [SerializeField] private float keyBindingsPanelWidth = 0f;

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

    [Tooltip("Keep the menu (and the How to Play / Controls screens) between the black bars at the top and bottom of the screen: if it would be taller, the buttons or scrolling text shrink to fit.")]
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

    [Tooltip("Colour of the main menu's own buttons (Resume, Save / Load, Stats, Settings, Guide - their look on purpose). Every other button takes its colour from its role (PixelUITheme).")]
    [SerializeField] private Color menuButtonColor = new Color(0.2f, 0.45f, 0.9f, 1f);

    private Color textColor => PixelUITheme.TextColor;   // (the theme: PixelUITheme)

    [Header("Canvas")]
    [Tooltip("Sorting order of the canvas (above every other panel).")]
    [SerializeField] private int sortingOrder = 500;

    [Tooltip("Reference resolution for the canvas scaler.")]
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);

    /// <summary>True while the game is paused by this menu.</summary>
    public static bool IsPaused { get => isPaused; private set { isPaused = value; PixelGameMode.Refresh(); } }
    private static bool isPaused;

    /// <summary>True while the menu is open AND it has frozen the game (see the 'Pausing stops the game' setting).</summary>
    public static bool GameStopped { get; private set; }

    private const string PrefPauseStops = "PixelClicker.Setting.PauseStopsGame";

    /// <summary>Reads the "pause stops the game" setting again (a save file with its own settings was loaded).</summary>
    public void ReloadSettingsFromPrefs()
    {
        pauseStopsGame = PixelPrefs.GetInt(PrefPauseStops, 1) != 0;
    }
    private bool pauseStopsGame = true;

    private GameObject canvasRoot;
    private GameObject menuRoot;
    private GameObject mainPanel, statsPanel, settingsPanel, restartPanel, quitPanel;
    private Slider masterSlider, effectsSlider, musicSlider;
    private Toggle muteToggle;
    private PixelHoldButton restartHold;

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

    private struct MenuEntry
    {
        public string label;
        public Color color;      // used when role is Custom (the main menu's own blue buttons)
        public ButtonRole role;  // Custom (the default) = 'color'; Give feedback is Success, Quit is Danger
        public UnityEngine.Events.UnityAction action;

        public bool fullRow; // spans the whole row (Resume, Quit)
    }
    private ScrollRect statsScroll;
    private GameObject statsBar;
    private RectTransform statsContent;
    private readonly List<TMP_Text> statValueTexts = new List<TMP_Text>();
    private string statsSignature = "";
    private RectTransform statsPanelRect, statsRestartRect;
    private float statsContentHeight, statsListTop;
    private TMP_InputField statsSearchField;
    private GameObject keysPanel, playlistPanel;
    private Toggle muteMusicToggle;
    private readonly List<Toggle> playlistToggles = new List<Toggle>();
    private readonly List<System.Action> settingsRefreshers = new List<System.Action>();
    private TMP_Text[] keyButtonLabels;
    private int capturingAction = -1;
    private GameObject saveLoadPanel, overwriteOverlay;
    private TMP_Text overwriteText;
    private Button[] slotButtons;
    private TMP_Text[] slotLabels;
    private string[] slotDetails;
    private GameObject renameOverlay, slotTip;
    private TMP_InputField renameInput;
    private TMP_Text renameTitleLabel, slotTipLabel;
    private RawImage slotTipImage;
    private RectTransform slotTipImageRect;
    private int renameSlot = 1, hoverSlot = -1;

    [Tooltip("Rename button on each save slot.")]
    [SerializeField] private string renameButtonText = "Rename";
    [Tooltip("Title of the rename box. {0} = slot number.")]
    [SerializeField] private string renameTitle = "Rename slot {0}";
    [Tooltip("Placeholder of the rename box's text field.")]
    [SerializeField] private string renamePlaceholder = "Save name...";
    [Min(4)]
    [Tooltip("Longest name a save slot can have.")]
    [SerializeField] private int slotNameMaxLength = 24;

    /// <summary>How messages refer to a slot: its number, or its custom name in quotes ("slot {0}" reads 'slot 2' or 'slot "My run"').</summary>
    private string SlotRef(int slot)
    {
        string custom = saveGame != null ? saveGame.GetSlotName(slot) : "";
        return string.IsNullOrWhiteSpace(custom) ? slot.ToString() : "\"" + custom + "\"";
    }

    /// <summary>The player's name for a save slot, or the default 'Slot N'.</summary>
    private string SlotNameOf(int slot)
    {
        string custom = saveGame != null ? saveGame.GetSlotName(slot) : "";
        return string.IsNullOrWhiteSpace(custom) ? string.Format(slotNameFormat, slot) : custom;
    }
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
        Update_PlaylistPreview();
        UpdateSlotTip();
        UpdateSlotDeleteHold();
        if (IsPaused && UpdateKeyCapture()) return;

        if (floorPopout != null)
        {
            UpdateFloorPopout();
            if (EscapePressed()) CloseFloorPopout();
            return;
        }

        if (EscapePressed() && !PixelFeedback.Open && !PixelFeedback.ClosedThisFrame)
        {
            if (IsPaused && (PixelAutosaveWindow.CloseIfOpen() || PixelDevTools.ClosePanel() || PixelDatabaseWindow.CloseIfOpen()))
            {
                // The dev tools panel (opened from this menu) took the key press.
            }
            else if (IsPaused)
            {
                // Inside Stats / Settings, Escape steps back; from the main view it resumes.
                if (renameOverlay != null && renameOverlay.activeSelf) renameOverlay.SetActive(false);
                else if (resultOverlay != null && resultOverlay.activeSelf) resultOverlay.SetActive(false);
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

    // (the floor-style pop-out box's last position, kept for the session; the box itself is in PixelPauseMenuLooks.cs)
    private static Vector2 floorPopPosition = new Vector2(520f, 120f);

    /// <summary>The settings rows re-read their values (the floor picker shows the new style).</summary>
    private void settingsRefreshersRun()
    {
        foreach (System.Action refresh in settingsRefreshers) refresh?.Invoke();
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
        if (!paused) openedFromTitle = false;

        ApplyFreeze();

        if (menuRoot != null)
        {
            if (paused) menuRoot.SetActive(true);
            else
            {
                // Resuming: the open screen shrinks away, then the whole menu goes (time already runs again).
                GameObject open = OpenView();
                if (open != null && menuRoot.activeSelf) PixelPop.Hide(open, menuRoot);
                else menuRoot.SetActive(false);
            }
        }
        if (paused) { ClearMenuSearch(); ShowView(mainPanel); }
    }

    /// <summary>Shows a pause-menu screen with the grow animation, or hides it at once (screens swap instantly, the new one pops in).</summary>
    private static void SetView(GameObject panel, bool on)
    {
        if (panel == null) return;
        if (!on) { panel.SetActive(false); return; }
        if (panel.GetComponent<PixelPop>() == null) panel.AddComponent<PixelPop>();
        PixelPop.Show(panel);
    }

    /// <summary>The pause-menu screen that is showing now (null if none).</summary>
    private GameObject OpenView()
    {
        GameObject[] views =
        {
            mainPanel, statsPanel, settingsPanel, howToPlayScreen != null ? howToPlayScreen.panel : null,
            controlsScreen != null ? controlsScreen.panel : null, restartPanel, quitPanel, saveLoadPanel, keysPanel, playlistPanel
        };
        foreach (GameObject v in views) if (v != null && v.activeSelf) return v;
        return null;
    }

    private bool openedFromTitle;

    /// <summary>Opens the Settings screen straight from the title screen (Back / Escape return to the title).</summary>
    public static void OpenSettingsFromTitle() { PixelFind.First<PixelPauseMenu>()?.OpenFromTitle(true); }

    /// <summary>Opens the Settings screen straight from the title screen with the Audio section unfolded and scrolled into view (the speaker button's right click).</summary>
    public static void OpenAudioSettingsFromTitle() { PixelFind.First<PixelPauseMenu>()?.OpenAudioFromTitle(); }

    private void OpenAudioFromTitle()
    {
        if (IsPaused) return;
        OpenFromTitle(true);
        settingsFilter = "";
        if (settingsSearchField != null) settingsSearchField.SetTextWithoutNotify("");
        expandedSettingsSections.Add(headerAudio);
        LayoutSettings();
        foreach (KeyValuePair<RectTransform, string> h in settingsHeaders)
            if (h.Value == headerAudio && h.Key != null && settingsScroll != null)
            {
                float top = -h.Key.anchoredPosition.y;
                float max = Mathf.Max(0f, settingsScroll.content.sizeDelta.y - settingsListHeight);
                settingsScroll.content.anchoredPosition = new Vector2(0f, Mathf.Clamp(top - 8f, 0f, max));
                break;
            }
    }

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
        PixelSaveGame.HasLoadedSave = false;   // after the reload the main menu's Continue loads a save again
        if (saveGame != null && restartDeletesSave)
        {
            saveGame.SuppressSaving(); // so autosave / quit can't write the old game back
            saveGame.DeleteSave();
            PixelHints.ResetSeen(); // a new game shows the first-time tips (and the intro) again
            PixelSettingsSync.ResetToDefaults();   // ...and starts with the default settings
            PixelSaveGame.AutoStartNewGame = true;   // the main menu starts it by itself (no Continue detour)
            PixelSaveGame.NewGameKeepsDefaults = true;   // (and the new game keeps the default settings, whatever the old saves say)
        }
        PixelCrashLog.EndSessionCleanly(); // a deliberate restart is not a crash
        SetPaused(false);
        PixelGameState.NewGame();   // the player's progress starts fresh (GameState)
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

    /// <summary>A button in a ROLE (its colour comes from PixelUITheme), with the menu's text colour.</summary>
    private Button MakeButton(Transform parent, string objectName, string label, Vector2 size, ButtonRole role, float labelSize)
    {
        Button b = PixelUIKit.CreateButton(font, parent, objectName, label, size, PixelUITheme.ColorOf(role), textColor, labelSize);
        PixelUIKit.SetRole(b, role);
        return b;
    }

    /// <summary>A main-menu entry's button (or a search result): its role, or its own colour when the role is Custom.</summary>
    private Button MakeButton(Transform parent, string objectName, string label, Vector2 size, ButtonRole role, Color customColor, float labelSize)
        => role == ButtonRole.Custom ? MakeButton(parent, objectName, label, size, customColor, labelSize) : MakeButton(parent, objectName, label, size, role, labelSize);

    /// <summary>Shows / hides the whole pause menu canvas (the feedback window hides it for a frame to take a clean screenshot).</summary>
    public static void SetMenuVisible(bool visible)
    {
        PixelPauseMenu menu = PixelFind.First<PixelPauseMenu>();
        if (menu == null || menu.canvasRoot == null) return;
        Canvas c = menu.canvasRoot.GetComponent<Canvas>();
        if (c != null) c.enabled = visible;
    }

    private void BuildUI()
    {
        canvasRoot = PixelUIKit.CreateCanvas("PixelPauseMenu Canvas", sortingOrder, referenceResolution, true);
        // The music player, at the main menu's spot (bottom centre) but without the caption: shown when the menu opens and while hovered, gone quickly otherwise.
        if (showMusicPlayer)
            PixelTitleMusic.Create(canvasRoot.transform, font, pauseMusicHideSeconds > 0f ? pauseMusicHideSeconds : 1.5f, false,
                                   pauseMusicFadeSeconds > 0f ? pauseMusicFadeSeconds : 0.15f, () => IsPaused && !PixelTitleScreen.Showing);
        if (PixelFind.First<PixelFeedback>() == null) gameObject.AddComponent<PixelFeedback>();   // the Give feedback window

        // On-screen Pause button
        if (showPauseButtonOnScreen)
        {
            Button pause = MakeButton(canvasRoot.transform, "Pause Button", pauseButtonText, buttonSize,
                                      ButtonRole.Neutral, buttonFontSize);
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
        PixelUIKit.StyleWindow(panel.GetComponent<Image>(), WindowShade.Window);   // (the theme window colour, live)
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = panelSize;
        panelRect.anchoredPosition = Vector2.zero;

        // The same title plate as every menu screen: "Paused" on the left (after the ornament), the search box in its right end (where the X sits elsewhere).
        RectTransform plate = BuildTitlePlate(panel.transform);
        float plateSide = PlateHeight - 2f * PlatePad;
        float searchWidth = (mainPanelWidth - 2f * PlateMargin) * 0.5f;
        TMP_Text title = MakeText(plate, "Title", menuTitle, titleFontSize, FontStyles.Bold);
        PixelUIKit.Caps(title);
        title.raycastTarget = false;
        title.alignment = TextAlignmentOptions.MidlineLeft;
        title.enableAutoSizing = true;
        title.fontSizeMax = titleFontSize;
        title.fontSizeMin = 20f;
        RectTransform tr = title.rectTransform;
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(PlatePad + plateSide + 12f, 2f);
        tr.offsetMax = new Vector2(-(PlatePad + searchWidth + 12f), 0f);

        menuSearchField = PixelUIKit.CreateInputField(font, plate, "Menu Search", new Vector2(searchWidth, plateSide),
                                                      tickBoxColor, textColor, rowFontSize * 0.85f, menuSearchPlaceholder);
        RectTransform msr = menuSearchField.GetComponent<RectTransform>();
        msr.anchorMin = msr.anchorMax = msr.pivot = new Vector2(1f, 0.5f);
        msr.sizeDelta = new Vector2(searchWidth, plateSide);
        msr.anchoredPosition = new Vector2(-PlatePad, 0f);   // the same gaps as the X on the other screens
        menuSearchField.onValueChanged.AddListener(OnMenuSearchChanged);
        StyleSearchBox(menuSearchField);
        menuSearchTop = PlateMargin + PlateHeight + 20f;

        float y = PlateMargin + PlateHeight + 20f;
        List<MenuEntry> entries = new List<MenuEntry>();

        // Build every screen first, then list the buttons in the order they appear (two per row).
        if (showSaveLoad && saveGame != null) BuildSaveLoadPanel();
        BuildStatsPanel();
        BuildSettingsPanel();
        PixelMenuFx.DecorateAll(statsPanel != null ? statsPanel.transform : null);      // the fancy menu's light button animation on these two screens
        PixelMenuFx.DecorateAll(settingsPanel != null ? settingsPanel.transform : null);
        PixelMenuFx.DecorateAll(saveLoadPanel != null ? saveLoadPanel.transform : null);   // the Save / Load screen too (built before this point)
        BuildQuitPanel();
        BuildRestartPanel();
        if (showHowToPlay) howToPlayScreen = BuildGuideScreen("Game Guide Panel", guideMenuText, howToPlayResource, emptyHowToPlayText, false);
        if (showControls) controlsScreen = BuildGuideScreen("Controls Panel", guideMenuText, controlsResource, emptyControlsText, true);

        // Resume on top, alone
        entries.Add(new MenuEntry { label = resumeText, color = menuButtonColor, action = () => SetPaused(false), fullRow = true });
        // Save / Load | Stats
        if (saveLoadPanel != null) entries.Add(new MenuEntry { label = saveLoadMenuText, color = menuButtonColor, action = () => ShowView(saveLoadPanel) });
        if (showStats) entries.Add(new MenuEntry { label = statsText, color = menuButtonColor, action = () => ShowView(statsPanel) });
        // Settings | Controls
        if (showSettings) entries.Add(new MenuEntry { label = settingsText, color = menuButtonColor, action = () => ShowView(settingsPanel) });
        // How To Play (two tabs: Controls and Game Guide)
        if (controlsScreen != null || howToPlayScreen != null) entries.Add(new MenuEntry { label = guideMenuText, color = menuButtonColor, action = () => ShowView((controlsScreen != null ? controlsScreen : howToPlayScreen).panel) });
        // Give feedback: a long green button right above Quit
        entries.Add(new MenuEntry { label = string.IsNullOrEmpty(feedbackText) ? "Give feedback" : feedbackText, role = ButtonRole.Success, action = () => PixelFeedback.OpenWindow(), fullRow = true });
        // Quit at the bottom, alone
        if (showQuit) entries.Add(new MenuEntry { label = quitText, role = ButtonRole.Danger, action = () => ShowView(quitPanel), fullRow = true });

        int buttonsBefore = panel.transform.childCount;
        LayoutMainButtons(panel.transform, entries, y, panelRect);
        mainButtons.Clear();
        for (int c = buttonsBefore; c < panel.transform.childCount; c++) mainButtons.Add(panel.transform.GetChild(c).gameObject);
        mainEntries = entries;

        PixelMenuFx.BuildBackdrop(menuRoot.transform, panelRect);   // the fancy menu's motes, vignette and panel glow (hidden when the setting is off)

        menuRoot.SetActive(false);
    }

    // ---- Inspector values used by the part files (declared here, in their old order, so the Inspector order stays the same) ----

    [Tooltip("Colour of the sunken title plate behind every menu screen's title (Settings, Stats, Save / Load...).")]
    [SerializeField] private Color titlePlateColor = new Color(0.02f, 0.03f, 0.06f, 0.75f);

    [Tooltip("Hover tip on Settings rows whose change only applies after the game is restarted.")]
    [SerializeField] private string restartTipText = "Requires a restart to take effect.";

    [Min(0f)]
    [Tooltip("Seconds the right mouse button is held on a save slot to delete its save. 0 = the coded default (1.4).")]
    [SerializeField] private float deleteSlotHoldSeconds = 0f;

    [Tooltip("Placeholder of the search box at the top right of the pause menu.")]
    [SerializeField] private string menuSearchPlaceholder = "Search menu and settings...";
    [Tooltip("Grey hint text in the Settings search box.")]
    [SerializeField] private string settingsSearchPlaceholder = "Search settings...";

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
                                       entries[i].role, entries[i].color, menuButtonFontSize);
            RectTransform rt = button.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(x, -(top + rowOf[i] * (buttonHeight + menuButtonSpacing)));
            button.onClick.AddListener(entries[i].action);

            // The fancy pause menu (PixelMenuFx): an animated button with an accent stripe, a shine, an icon for a few entries, and a staggered entrance. It hides itself when the setting is off.
            Texture2D fancyIcon = entries[i].label == saveLoadMenuText ? PixelMenuIcons.Floppy : entries[i].label == settingsText ? PixelMenuIcons.Cog : entries[i].label == quitText ? PixelMenuIcons.Door : null;
            PixelMenuFx.Decorate(button, Color.Lerp(entries[i].role == ButtonRole.Custom ? entries[i].color : PixelUITheme.ColorOf(entries[i].role), Color.white, 0.55f), fancyIcon, i);
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
        float below = 30f; // room under the list (the Back button is now an X at the top)
        return Mathf.Max(120f, Mathf.Min(desired, AvailableHeight() - yTop - below));
    }

    private void AddMenuButton(Transform parent, string label, ButtonRole role, ref float y, UnityEngine.Events.UnityAction onClick)
    {
        Button button = MakeButton(parent, label + " Button", label, menuButtonSize, role, menuButtonFontSize);
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
        ClipFillToButton(fillRect);
        seconds = Mathf.Max(0.05f, holdSeconds);
        onComplete = complete;
        ResetProgress();
    }

    /// <summary>
    /// A rounded (fancy) button has rounded corners, but the fill is a plain rectangle that would poke out past them. The fill is moved into a mask
    /// drawn with the button's own sprite, so it is cut to the exact rounded shape.
    /// </summary>
    private void ClipFillToButton(RectTransform fillRect)
    {
        Image face = GetComponent<Image>();
        if (face == null || face.sprite == null || fillRect == null || fillRect.parent != transform) return;
        GameObject clip = new GameObject("Hold Clip", typeof(RectTransform), typeof(Image), typeof(Mask));
        clip.transform.SetParent(transform, false);
        clip.transform.SetSiblingIndex(fillRect.GetSiblingIndex());
        Image mask = clip.GetComponent<Image>();
        mask.sprite = face.sprite;
        mask.type = face.type;
        mask.pixelsPerUnitMultiplier = face.pixelsPerUnitMultiplier;
        mask.color = Color.white;
        mask.raycastTarget = false;
        clip.GetComponent<Mask>().showMaskGraphic = false;
        PixelUIKit.Stretch(clip.GetComponent<RectTransform>());
        fillRect.SetParent(clip.transform, false);
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

/// <summary>Drags a UI box by this handle (the pause menu's floor-style pop-out).</summary>
public class PixelPopoutDrag : MonoBehaviour, UnityEngine.EventSystems.IBeginDragHandler, UnityEngine.EventSystems.IDragHandler, UnityEngine.EventSystems.IEndDragHandler
{
    public System.Action onBegin;   // a drag starts (a docked window undocks itself here, so it can be carried off by its header)
    public void OnBeginDrag(UnityEngine.EventSystems.PointerEventData e) { onBegin?.Invoke(); }
    public RectTransform target;
    public Canvas canvas;
    public System.Action onMoved;
    public System.Action onEnd;   // the drag was let go (My Window docks to an edge here)

    public void OnEndDrag(UnityEngine.EventSystems.PointerEventData e) { onEnd?.Invoke(); }
    public bool locked;   // 'stuck' boxes can't be dragged

    public void OnDrag(UnityEngine.EventSystems.PointerEventData e)
    {
        if (target == null || locked) return;
        float scale = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
        target.anchoredPosition += e.delta / scale;
        onMoved?.Invoke();
    }
}
