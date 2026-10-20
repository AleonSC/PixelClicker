using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Developer tools for Pixel Clicker. Adds a "Dev Tools" button to the pause menu (only in the Editor and Development
/// Builds) that opens a panel with: (Tick "Allow In Full Build" on the component to keep it in a normal build, e.g. for a tester.)
///   - add pixels: pick a pixel type from a dropdown, type an amount, press Add
///   - add the "all pixels" amount to every unlocked pixel type
///   - spawn a minigame: pick one from a dropdown, press Spawn
///   - unlock every locked item in the shop
///   - a tick box that lets you press Q to clear the old pixels
///
/// Add it to any GameObject. By default it removes itself in non-development builds (and the pause menu button
/// disappears with it).
/// </summary>
public class PixelDevTools : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The PixelClicker to add to. Found automatically if left empty.")]
    [SerializeField] private PixelClicker clicker;

    [Tooltip("The shop (for Unlock All). Found automatically if left empty.")]
    [SerializeField] private PixelShop shop;

    [Tooltip("Fallback font, used only when the PixelClicker's 'UI Font' is empty. Empty = TextMeshPro default font.")]
    [SerializeField] private TMP_FontAsset font;

    [Header("Cheats")]
    [Tooltip("Remember the tick boxes, the amount and the drop-down choices between play sessions (PlayerPrefs). The values set in the Inspector are the first-time defaults.")]
    [SerializeField] private bool rememberSettings = true;

    [Tooltip("Start with 'Disable old pixel despawns' ticked: old pixels never expire on their own.")]
    [SerializeField] private bool disableDespawn = false;

    [Tooltip("Start with 'Disable old pixel limit' ticked: any number of old pixels can lie around (the Old Pixel Capacity cap is ignored).")]
    [SerializeField] private bool disableOldPixelLimit = false;

    [Tooltip("Start with 'God pixel mode' ticked: the mouse spawns pixels (left click) and destroys them (right click); scroll to choose the pixel.")]
    [SerializeField] private bool godMode = false;

    [Min(1f)]
    [Tooltip("God pixel mode: pixels spawned per second while the left button is held.")]
    [SerializeField] private float godSpawnRate = 14f;

    [Min(5f)]
    [Tooltip("God pixel mode: how close (screen pixels) an old pixel must be to the cursor to be destroyed by a right click.")]
    [SerializeField] private float godEraseRadius = 70f;

    [Tooltip("God pixel mode: where the small display sits relative to the cursor (canvas units).")]
    [SerializeField] private Vector2 godDisplayOffset = new Vector2(34f, -34f);

    [Tooltip("God pixel mode: text size of the small display.")]
    [SerializeField] private float godFontSize = 24f;

    [Tooltip("Does the 'All pixels' add option also include tiers that aren't unlocked yet? Off = only unlocked tiers.")]
    [SerializeField] private bool includeLockedTiers = false;

    [Tooltip("The Add buttons also give the same amount of Ultra pixels of that type (rounded down to a whole number).")]
    [SerializeField] private bool alsoAddUltra = true;

    [Tooltip("Amount filled into the text field when the panel is first built.")]
    [SerializeField] private string defaultAmount = "100";

    [Tooltip("Start with the 'Press Q to clear old pixels' box ticked.")]
    [SerializeField] private bool clearKeyEnabled = false;

    [Tooltip("Start with 'Infinite resources' ticked: everything in the shop and crafting costs nothing.")]
    [SerializeField] private bool infiniteResources = false;

    [Tooltip("Start with the on-screen 'spawn selector' ticked: a drop-down at the top of the screen that makes only the chosen pixel (or the Monochrome / RGB pack) spawn.")]
    [SerializeField] private bool spawnSelectorEnabled = false;

    [Tooltip("TICK THIS to let the dev tools work in a normal (full) build too, e.g. one you give to a tester. Unticked, the dev tools only exist in the Editor and Development Builds, and the Dev Tools button is missing from the pause menu in a full build.")]
    [SerializeField] private bool allowInFullBuild = false;

    [Header("Text")]
    [Tooltip("Title of the dev tools panel (also the text of the pause menu button).")]
    [SerializeField] private string title = "Dev Tools";

    [Header("To-Do tab")]
    [Tooltip("Name of the text file in Assets/Resources (without .txt) shown in the To-Do tab. One item per line: '[ ] text' = open, '[x] text' = done, '# Heading', '//' = hidden note.")]
    [SerializeField] private string todoResource = "Todo";




    [Tooltip("Name of the text file in Assets/Resources (without .txt) shown in the Changelog tab, one change per line, newest first.")]
    [SerializeField] private string changelogResource = "Changelog";

    [Tooltip("Colour of finished to-do items.")]
    [SerializeField] private Color todoDoneColor = new Color(0.45f, 0.8f, 0.5f, 1f);

    [Tooltip("Tooltip of the green plus button next to the amount field.")]
    [SerializeField] private string addTipText = "Add this amount of the selected pixel (All pixels = every type), plus the same number of Ultra pixels";

    [Tooltip("Tooltip of the yellow paw button next to the plus button.")]
    [SerializeField] private string petTipText = "Give the pet of the selected pixel (All pixels = every pet, quietly, switched off)";

    [Tooltip("First entry of the pixel drop-down: adds the amount to every pixel type.")]
    [SerializeField] private string allPixelsText = "All pixels";

    [Tooltip("Label of the disable-despawns tick box.")]
    [SerializeField] private string noDespawnToggleText = "Disable old pixel despawns";

    [Tooltip("Label of the tick box that removes the limit on old pixels.")]
    [SerializeField] private string noLimitToggleText = "Disable old pixel limit";

    [Tooltip("Label of the tick box that makes the Farmer bring a Dragon Seed on every visit.")]
    [SerializeField] private string farmerSeedsToggleText = "Farmer always has dragon seeds";

    private bool farmerAlwaysSeeds;
    private bool doubleFloor;
    private Toggle doubleFloorToggle;

    [Tooltip("Label of the tick box that doubles the floor circle (picture, wireframe, borders and the limits for pixels and the first person player).")]
    [SerializeField] private string doubleFloorToggleText = "Double floor area";
    private Toggle farmerSeedsToggle;

    [Tooltip("Label of the god-pixel-mode tick box.")]
    [SerializeField] private string godModeLabel = "God Pixel Mode";

    [Tooltip("Tooltip of the god-pixel-mode row (shown while the mouse is over it).")]
    [SerializeField] private string godTipText = "Scroll = pick a pixel, left click = spawn it, right click = destroy old pixels near the cursor";

    [Tooltip("Label of the minigame spawn button.")]
    [SerializeField] private string spawnText = "Spawn";

    [Tooltip("Tooltip of the file button at the top left that opens the folder with the crash / error reports (for sending to the developer).")]
    [SerializeField] private string reportTipText = "Open the crash / error report folder";

    [Tooltip("Text of the button that sets every pixel count to 0 (inventory, Log and Bank) but keeps unlocks.")]
    [SerializeField] private string resetCountsText = "Reset all counts to 0";

    [Tooltip("Text the reset button shows after the first click (click again within a few seconds to confirm).")]
    [SerializeField] private string resetConfirmText = "Click again to erase all counts";

    [Tooltip("Label of the clear-old-pixels tick box.")]
    [SerializeField] private string clearToggleText = "Press Q to clear old pixels";

    [Tooltip("Label of the infinite-resources tick box.")]
    [SerializeField] private string infiniteToggleText = "Infinite resources";


    [Tooltip("First entry of the on-screen selector (normal random spawning).")]
    [SerializeField] private string spawnNormalText = "Spawn: normal (random)";

    [Tooltip("Entry that spawns only White, Gray and Black.")]
    [SerializeField] private string spawnMonochromeText = "Spawn: Monochrome pack";

    [Tooltip("Entry that spawns only Red, Green and Blue.")]
    [SerializeField] private string spawnRgbText = "Spawn: RGB pack";






    [Tooltip("Prefix of the middle-click swap drop-down entries.")]
    [SerializeField] private string swapPrefix = "Middle click: ";

    [Tooltip("Text of the Despawn button (removes the selected minigame's current round).")]
    [SerializeField] private string despawnText = "Despawn";

    [Tooltip("Text of the button that drinks the focus potion picked in the 'Focus potions' widget.")]
    [SerializeField] private string drinkFocusText = "Drink";

    [Tooltip("Text of the button that cancels the active focus potion.")]
    [SerializeField] private string cancelFocusText = "Cancel";

    [Tooltip("Text of the button that unlocks the shop pack picked in the 'Shop unlocks' widget (or every pack when 'All' is picked).")]
    [SerializeField] private string shopUnlockText = "Unlock";

    [Tooltip("Text of the button that relocks the shop pack picked in the 'Shop unlocks' widget (or every pack when 'All' is picked).")]
    [SerializeField] private string shopRelockText = "Relock";

    [Tooltip("First entry of the 'Unlock' drop-down: every shop pack and every pixel.")]
    [SerializeField] private string shopAllText = "All shop items";
    [Tooltip("Text of the 'Max XP' widget's button that fills the picked skill(s) to their level cap.")]
    [SerializeField] private string maxXpText = "Max XP";
    [Tooltip("Label of the 'Husk pile' widget's button: drops husks round the cube until the floor limit is reached (for profiling a full floor).")]
    [SerializeField] private string fillFloorText = "Fill floor";
    [Min(1)]
    [Tooltip("Husks the 'Fill floor' button drops per frame (spread over frames so the test isn't one giant spike).")]
    [SerializeField] private int fillFloorPerFrame = 6;
    [Tooltip("Text of the 'Max XP' widget's button that takes the picked skill(s) back to level 1.")]
    [SerializeField] private string resetXpText = "Reset XP";
    [Tooltip("First entry of the 'Max XP' drop-down: acts on every skill.")]
    [SerializeField] private string allSkillsText = "All skills";

    [Tooltip("Text of the 'Super Rares' widget's button that puts the picked rare cube on the clicker cube (replacing the block there).")]
    [SerializeField] private string spawnRareText = "Spawn";
    [Tooltip("Text of the 'Super Rares' widget's button that puts a Prime Gauntlet in your inventory.")]
    [SerializeField] private string giveGauntletText = "Gauntlet";

    [Tooltip("Text of the 'Dev items' widget's button that puts the picked dev-only device in your inventory.")]
    [SerializeField] private string giveDevItemText = "Give";

    [Tooltip("Label of the click-count row (how many clicks one click counts as).")]
    [SerializeField] private string clickCountText = "Clicks per click";

    [Header("Look")]


    [Tooltip("Height of each row.")]
    [SerializeField] private float rowHeight = 64f;

    [Tooltip("Text size of rows and buttons.")]
    [SerializeField] private float fontSize = 30f;



    [Tooltip("Button colour.")]
    [SerializeField] private Color buttonColor = new Color(0.8f, 0.25f, 0.25f, 1f);

    [Tooltip("Dropdown / text field box colour.")]
    [SerializeField] private Color boxColor = new Color(0.22f, 0.22f, 0.28f, 1f);

    [Tooltip("Dropdown list colour.")]
    [SerializeField] private Color listColor = new Color(0.16f, 0.16f, 0.2f, 1f);

    [Tooltip("Text colour.")]
    [SerializeField] private Color textColor = Color.white;

    [Tooltip("Tick box colours (box, tick).")]
    [SerializeField] private Color tickBoxColor = new Color(0.25f, 0.25f, 0.3f, 1f);
    [SerializeField] private Color tickColor = new Color(0.4f, 0.9f, 0.5f, 1f);

    [Header("Canvas")]

    [Tooltip("Reference resolution for the canvas scaler.")]
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);

    private static PixelDevTools instance;
    private GameObject canvasRoot;
    private TMP_Dropdown pixelDropdown;
    private int[] pixelMap;   // option -> entry (0 = All pixels, n = tier n - 1): the list is alphabetical
    /// <summary>The tier index picked in the pixel drop-down (-1 = All pixels).</summary>
    private int PickedPixelTier => PixelUIKit.Original(pixelMap, pixelDropdown.value) - 1;
    private TMP_InputField amountField;
    private readonly List<PixelMinigame> dropdownMinigames = new List<PixelMinigame>();

    /// <summary>True when dev tools exist (Editor / development build). The pause menu uses this to show its button.</summary>
    public static bool Available => instance != null;

    /// <summary>The text for the pause menu's button.</summary>
    public static string ButtonText => instance != null ? instance.title : "Dev Tools";

    /// <summary>Is the dev tools panel showing?</summary>
    public static bool IsOpen => PixelDevHub.IsOpen;

    /// <summary>Opens the panel (called by the pause menu button).</summary>
    public static void OpenPanel() { if (instance != null) instance.Open(); }

    /// <summary>Closes the panel if it is showing. Returns true if it was.</summary>
    public static bool ClosePanel()
    {
        if (!IsOpen) return false;
        instance.Close();
        return true;
    }

    private void Awake()
    {
        if (!allowInFullBuild && !Debug.isDebugBuild) { Destroy(this); return; }
        instance = this;
        LoadSettings();
        PixelClicker.InfiniteResources = infiniteResources;
        OldPixelDespawn.DevNoDespawn = disableDespawn;
        PixelClicker.DevNoOldPixelLimit = disableOldPixelLimit;
        PixelClicker.GodMode = godMode;
        PixelClicker.DevClickCount = clickCount;
    }

    // ------------------------------------------------------------------
    // Remembered settings (PlayerPrefs)
    // ------------------------------------------------------------------

    private const string PrefPrefix = "PixelClicker.Dev.";

    private bool GetBool(string key, bool fallback) =>
        !rememberSettings ? fallback : PixelPrefs.GetInt(PrefPrefix + key, fallback ? 1 : 0) != 0;

    private void SetBool(string key, bool value)
    {
        if (!rememberSettings) return;
        PixelPrefs.SetInt(PrefPrefix + key, value ? 1 : 0);
        PixelPrefs.Save();
    }

    // Every remembered Dev Tools choice, per save file (PixelSettingsSync stores each declared key in the save). A new remembered choice is
    // added HERE with its kind, so it can never be left out of the save again (the drop-downs added later once were).
    private static readonly string[] IntPrefs =
        { "ClearKey", "Infinite", "Selector", "NoDespawn", "NoLimit", "FarmerSeeds", "DoubleFloor", "God", "ClickCount",
          "PixelIndex", "SelPixel", "SelMinigame", "GodChoice", "SelSwap", "SelFocus", "SelShopPack", "SelSkillXp", "SelRare" };
    private static readonly string[] StringPrefs = { "Amount" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void DeclarePerSavePrefs()
    {
        foreach (string k in IntPrefs) PixelPrefs.DeclarePerSave(PrefPrefix + k, PixelPrefs.Kind.Int);
        foreach (string k in StringPrefs) PixelPrefs.DeclarePerSave(PrefPrefix + k, PixelPrefs.Kind.String);
    }

    private int GetInt(string key, int fallback) => !rememberSettings ? fallback : PixelPrefs.GetInt(PrefPrefix + key, fallback);

    private void SetInt(string key, int value)
    {
        if (!rememberSettings) return;
        PixelPrefs.SetInt(PrefPrefix + key, value);
        PixelPrefs.Save();
    }

    private int clickCount = 1;
    private TMP_InputField clickCountField;
    private Toggle clearToggle, infiniteToggle, noDespawnToggle, noLimitToggle, godToggle;

    /// <summary>A save file with its own settings was loaded: read the dev tool choices again and update the panel.</summary>
    public static void ReloadFromPrefs()
    {
        if (instance != null) instance.ApplyLoadedSettings();
    }

    private void ApplyLoadedSettings()
    {
        LoadSettings();
        PixelClicker.InfiniteResources = infiniteResources;
        OldPixelDespawn.DevNoDespawn = disableDespawn;
        PixelClicker.DevNoOldPixelLimit = disableOldPixelLimit;
        PixelClicker.DevClickCount = clickCount;
        if (clickCountField != null) clickCountField.SetTextWithoutNotify(clickCount.ToString());
        if (clearToggle != null) clearToggle.SetIsOnWithoutNotify(clearKeyEnabled);
        if (infiniteToggle != null) infiniteToggle.SetIsOnWithoutNotify(infiniteResources);
        if (noDespawnToggle != null) noDespawnToggle.SetIsOnWithoutNotify(disableDespawn);
        if (noLimitToggle != null) noLimitToggle.SetIsOnWithoutNotify(disableOldPixelLimit);
        if (farmerSeedsToggle != null) farmerSeedsToggle.SetIsOnWithoutNotify(farmerAlwaysSeeds);
        if (doubleFloorToggle != null) doubleFloorToggle.SetIsOnWithoutNotify(doubleFloor);
        if (godToggle != null) godToggle.SetIsOnWithoutNotify(godMode);
        if (amountField != null) amountField.SetTextWithoutNotify(defaultAmount);
        if (pixelDropdown != null && pixelDropdown.options.Count > 0)
        {
            pixelDropdown.SetValueWithoutNotify(Mathf.Clamp(GetInt("PixelIndex", 0), 0, pixelDropdown.options.Count - 1));
            pixelDropdown.RefreshShownValue();
        }
        if (clicker == null) return; // the panel isn't built yet: Start applies everything
        if (selectorDropdown != null) selectorDropdown.SetValueWithoutNotify(Mathf.Clamp(GetInt("SelPixel", 0), 0, Mathf.Max(0, selectorDropdown.options.Count - 1)));
        if (selectorMinigameDropdown != null) selectorMinigameDropdown.SetValueWithoutNotify(Mathf.Clamp(GetInt("SelMinigame", 0), 0, Mathf.Max(0, selectorMinigameDropdown.options.Count - 1)));
        if (focusDropdown != null) focusDropdown.SetValueWithoutNotify(Mathf.Clamp(GetInt("SelFocus", 0), 0, Mathf.Max(0, focusDropdown.options.Count - 1)));
        ReselectFromPrefs(swapDropdown, "SelSwap");
        ReselectFromPrefs(shopUnlockDropdown, "SelShopPack");
        ReselectFromPrefs(skillXpDropdown, "SelSkillXp");
        ReselectFromPrefs(rareDropdown, "SelRare");
        if (selectorDropdown != null) { if (spawnSelectorEnabled) ApplySelector(selectorDropdown.value); else clicker.SetDevSpawnTiers(null); RefreshSpawnedPixelButton(); }
        SetGodMode(godMode);
    }

    /// <summary>Puts a drop-down back on the choice remembered for this save (after a save is loaded).</summary>
    private void ReselectFromPrefs(TMP_Dropdown dd, string key)
    {
        if (dd == null || dd.options.Count == 0) return;
        dd.SetValueWithoutNotify(Mathf.Clamp(GetInt(key, 0), 0, dd.options.Count - 1));
        dd.RefreshShownValue();
    }

    private void LoadSettings()
    {
        clearKeyEnabled = GetBool("ClearKey", clearKeyEnabled);
        infiniteResources = GetBool("Infinite", infiniteResources);
        spawnSelectorEnabled = GetBool("Selector", spawnSelectorEnabled);
        disableDespawn = GetBool("NoDespawn", disableDespawn);
        disableOldPixelLimit = GetBool("NoLimit", disableOldPixelLimit);
        farmerAlwaysSeeds = GetBool("FarmerSeeds", farmerAlwaysSeeds);
        PixelFarmerMinigame.DevAlwaysDragonSeeds = farmerAlwaysSeeds;
        doubleFloor = GetBool("DoubleFloor", doubleFloor);
        PixelFloor.DoubleArea = doubleFloor;
        godMode = GetBool("God", godMode);
        clickCount = Mathf.Max(1, GetInt("ClickCount", 1));
        if (rememberSettings) defaultAmount = PixelPrefs.GetString(PrefPrefix + "Amount", defaultAmount);
    }

    private void Start()
    {
        if (instance != this) return;

        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
        if (shop == null) shop = PixelFind.First<PixelShop>();
        if (clicker == null)
        {
            Debug.LogError("PixelDevTools: no PixelClicker found in the scene.", this);
            enabled = false;
            return;
        }

        if (clicker.UIFont != null) font = clicker.UIFont; // one shared font for the whole game
        PixelUIKit.EnsureEventSystem();
        BuildPanel();
    }

    private void OnDestroy()
    {
        PixelDevHub.OpenChanged -= OnHubOpenChanged;
        PixelDevDocs.Shown -= OnDocsShown;
        if (instance == this)
        {
            instance = null;
            PixelClicker.InfiniteResources = false;
            PixelClicker.DevClickCount = 1;
            PixelFarmerMinigame.DevAlwaysDragonSeeds = false;
        }
        if (godRoot != null) Destroy(godRoot);
        PixelClicker.GodMode = false;
        OldPixelDespawn.DevNoDespawn = false;
        PixelClicker.DevNoOldPixelLimit = false;
        if (spawnSelectorEnabled && clicker != null && clicker.isActiveAndEnabled) clicker.SetDevSpawnTiers(null);
    }

    private TMP_Dropdown swapDropdown;
    private readonly List<PixelClicker.PixelType> swapChoices = new List<PixelClicker.PixelType>();

    /// <summary>Middle click (with the on-screen selector on) swaps the pixel on the cube for the one chosen in the swap drop-down.</summary>
    private void UpdateMiddleSwap()
    {
        if (clicker == null || swapDropdown == null || swapDropdown.value <= 0 || PixelCalm.InMenu) return;
        if (!PixelInput.MiddlePressed() || PixelInput.PointerOverUI() || IsTyping()) return;
        int v = swapDropdown.value - 1;   // (entry 0 = 'off')
        if (v < 0 || v >= swapChoices.Count) return;
        clicker.DevSwapCurrentPixel(swapChoices[v]);
    }

    private TMP_Text sessionLabel;
    private int sessionShownSecond = -1;

    /// <summary>The session recorder button shows Start, or Stop with the time recorded so far.</summary>
    private void RefreshSessionLabel()
    {
        if (sessionLabel == null) return;
        if (!PixelSessionRecorder.Recording) { sessionLabel.text = sessionStartLabel; sessionShownSecond = -1; return; }
        int s = Mathf.FloorToInt(PixelSessionRecorder.ElapsedSeconds);
        if (s == sessionShownSecond) return;
        sessionShownSecond = s;
        sessionLabel.text = sessionStopLabel.Replace("{time}", (s / 60).ToString("00") + ":" + (s % 60).ToString("00"));
    }

    private void Update()
    {
        if (sessionLabel != null && (PixelSessionRecorder.Recording || sessionShownSecond != -1)) RefreshSessionLabel();
        if (!PixelTitleScreen.Showing)
        {
            if (PixelKeys.Pressed(PixelAction.DevTools)) { if (IsOpen) Close(); else Open(); }
            if (PixelKeys.Pressed(PixelAction.MarkMoment)) PixelDevProbe.MarkMoment();
            if (PixelKeys.Pressed(PixelAction.InspectHover)) PixelDevProbe.Inspect();
        }
        UpdateGod();
        UpdateMiddleSwap();
        if (!clearKeyEnabled || clicker == null || PixelPauseMenu.IsPaused) return;
        if (IsTyping()) return;
        if (PixelInput.QPressed()) clicker.ClearOldPixels();
    }

    private static bool IsTyping()
    {
        EventSystem es = EventSystem.current;
        if (es == null || es.currentSelectedGameObject == null) return false;
        TMP_InputField f = es.currentSelectedGameObject.GetComponent<TMP_InputField>();
        return f != null && f.isFocused;
    }

    // ------------------------------------------------------------------
    // Actions (also callable from other scripts or a UnityEvent)
    // ------------------------------------------------------------------

    /// <summary>Adds the amount to every (eligible) pixel type.</summary>
    public void AddToAll(double amount)
    {
        PixelClicker.PixelTier[] tiers = clicker.Tiers;
        for (int i = 0; i < tiers.Length; i++)
        {
            if (!includeLockedTiers && !tiers[i].unlocked) continue;
            GiveAmount(i, amount);
        }
    }

    /// <summary>Adds pixels of one type, and (if ticked in the Inspector) the same number of Ultra pixels.</summary>
    private void GiveAmount(int tierIndex, double amount)
    {
        clicker.AddCurrency(tierIndex, amount);
        if (alsoAddUltra && amount >= 1d) clicker.AddUltra((long)System.Math.Floor(amount));
    }

    private void AddSelected()
    {
        if (pixelDropdown == null || amountField == null) return;
        string text = amountField.text.Trim().Replace(",", "");
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double amount)) return;
        if (PickedPixelTier < 0) AddToAll(amount); // "All pixels"
        else GiveAmount(PickedPixelTier, amount);
    }


    [Header("Dev reports widget (short labels for its half-width buttons; the long explanations are in the hover tips)")]
    [Tooltip("Dev reports button: opens the crash report folder.")]
    [SerializeField] private string reportsCrashLabel = "Crash folder";
    [Tooltip("Dev reports button: snapshot of the view bounds.")]
    [SerializeField] private string reportsBoundsLabel = "Bounds";
    [Tooltip("Dev reports button: mark this moment (screenshot + report).")]
    [SerializeField] private string reportsMomentLabel = "Mark moment";
    [Tooltip("Dev reports button: UI layout dump.")]
    [SerializeField] private string reportsUiLabel = "UI dump";
    [Tooltip("Dev reports button: skill balance report.")]
    [SerializeField] private string reportsBalanceLabel = "Balance";

    [Header("Session recorder")]
    [Tooltip("Label of the session recorder button while nothing is recorded (PixelSessionRecorder).")]
    [SerializeField] private string sessionStartLabel = "Record session";
    [Tooltip("Label of the session recorder button while recording ({time} = game time recorded so far).")]
    [SerializeField] private string sessionStopLabel = "Stop & save {time}";

    [Header("Balance report")]
    [Min(0.1f)]
    [Tooltip("Manual clicks per second the balance report assumes.")]
    [SerializeField] private float balanceManualCps = 4f;

    [Min(0.1f)]
    [Tooltip("Auto clicks per second the balance report assumes (auto clicks earn only a share of the XP).")]
    [SerializeField] private float balanceAutoCps = 2f;

    [Tooltip("Skill levels the balance report compares (ascending; the second one is the level the outlier check judges).")]
    [SerializeField] private int[] balanceLevels = { 1, 10, 20, 30 };

    [Header("Dev reports delay")]
    [Min(0f)]
    [Tooltip("Seconds between pressing the snapshot button and the snapshot, so the panel can be closed and the view set up.")]
    [SerializeField] private float boundsSnapshotDelay = 3f;

    private System.Collections.IEnumerator ProbeRoutine(System.Action run, string what)
    {
        Debug.Log(what + " in " + boundsSnapshotDelay + " s - close this panel and set up what you want to show.");
        yield return new WaitForSecondsRealtime(boundsSnapshotDelay);
        yield return new WaitForEndOfFrame();
        run();
    }

    // ------------------------------------------------------------------
    // UI
    // ------------------------------------------------------------------

    private void Open()
    {
        PixelDevHub.Open();   // (the Dev Hub fills the drop-downs when it opens)
    }

    private GameObject todoView, changelogView;
    private TMP_Text changelogLabel;
    private ScrollRect changelogScroll;
    private GameObject changelogBar;
    private RectTransform changelogViewRect;
    private RectTransform todoContent;
    private ScrollRect todoScroll;
    private GameObject todoBar;
    private RectTransform todoViewRect;


    /// <summary>The Changelog tab: the lines of Resources/Changelog.txt, newest first, in one scrolling text.</summary>
    private void BuildChangelogView(Transform box, float top)
    {
        changelogView = new GameObject("Changelog View", typeof(RectTransform));
        changelogView.transform.SetParent(box, false);
        PixelUIKit.Stretch(changelogView.GetComponent<RectTransform>());

        changelogScroll = PixelUIKit.CreateScrollView(changelogView.transform, "Changelog Scroll", new Color(0.5f, 0.5f, 0.6f, 1f), 16f, 60f,
                                                      out RectTransform content, out changelogBar);
        changelogViewRect = changelogScroll.GetComponent<RectTransform>();
        changelogViewRect.anchorMin = Vector2.zero;
        changelogViewRect.anchorMax = Vector2.one;
        changelogViewRect.offsetMin = new Vector2(40f, 30f);
        changelogViewRect.offsetMax = new Vector2(-40f, -top);

        changelogLabel = PixelUIKit.CreateText(font, content, "Changelog Text", "", fontSize * 0.8f, TextAlignmentOptions.TopLeft, FontStyles.Normal, textColor);
        changelogLabel.raycastTarget = false;
        RectTransform lr = changelogLabel.rectTransform;
        lr.anchorMin = new Vector2(0f, 1f);
        lr.anchorMax = new Vector2(1f, 1f);
        lr.pivot = new Vector2(0.5f, 1f);
        lr.offsetMin = new Vector2(0f, 0f);
        lr.offsetMax = new Vector2(-30f, 0f);
    }

    private void RebuildChangelog()
    {
        if (changelogLabel == null) return;
        TextAsset asset = Resources.Load<TextAsset>(changelogResource);
        List<string> entries = new List<string>();
        if (asset != null)
            foreach (string raw in asset.text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length > 0) entries.Add(line);
            }
        entries.Reverse();   // newest first
        string text = entries.Count > 0 ? string.Join("\n\n", entries) : "Nothing in the changelog yet.";
        changelogLabel.text = text;
        float width = Mathf.Max(100f, changelogViewRect.rect.width - 30f);
        float height = Mathf.Ceil(changelogLabel.GetPreferredValues(text, width, 0f).y) + 10f;
        changelogLabel.rectTransform.sizeDelta = new Vector2(-30f, height);
        PixelUIKit.UpdateScrollView(changelogScroll, changelogBar, height, changelogViewRect.rect.height);
    }

    /// <summary>The To-Do tab: a scrolling list of the lines in Resources/Todo.txt, plus a Close button.</summary>
    private void BuildTodoView(Transform box, float top, float inner)
    {
        todoView = new GameObject("Todo View", typeof(RectTransform));
        todoView.transform.SetParent(box, false);
        PixelUIKit.Stretch(todoView.GetComponent<RectTransform>());

        todoScroll = PixelUIKit.CreateScrollView(todoView.transform, "Todo Scroll", new Color(0.5f, 0.5f, 0.6f, 1f), 16f, 60f,
                                                 out todoContent, out todoBar);
        todoViewRect = todoScroll.GetComponent<RectTransform>();
        todoViewRect.anchorMin = Vector2.zero;
        todoViewRect.anchorMax = Vector2.one;
        todoViewRect.offsetMin = new Vector2(40f, 30f);
        todoViewRect.offsetMax = new Vector2(-40f, -top);

    }

    private readonly List<GameObject> todoLines = new List<GameObject>();

    private void RebuildTodo()
    {
        if (todoContent == null) return;
        foreach (GameObject g in todoLines) if (g != null) Destroy(g);
        todoLines.Clear();

        TextAsset asset = Resources.Load<TextAsset>(todoResource);
        string[] lines = asset != null ? asset.text.Split('\n') : new string[0];
        float width = Mathf.Max(100f, todoViewRect.rect.width - 40f);
        float y = 0f;
        float size = fontSize * 0.85f;
        int count = 0;
        foreach (string raw in lines)
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("//")) continue;

            bool heading = line.StartsWith("#");
            bool done = line.StartsWith("[x]") || line.StartsWith("[X]");
            string text = heading ? line.TrimStart('#', ' ') : done ? "[x] " + line.Substring(3).Trim()
                        : line.StartsWith("[ ]") ? "[ ] " + line.Substring(3).Trim() : line;

            TMP_Text label = PixelUIKit.CreateText(font, todoContent, "Todo Line", text, heading ? size * 1.15f : size,
                                                   TextAlignmentOptions.TopLeft, heading ? FontStyles.Bold : FontStyles.Normal,
                                                   done ? todoDoneColor : textColor);
            label.raycastTarget = false;
            RectTransform rt = label.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            float height = label.GetPreferredValues(text, width, 0f).y;
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(0f, -y);
            y += height + 14f;
            todoLines.Add(label.gameObject);
            count++;
        }

        if (count == 0)
        {
            TMP_Text empty = PixelUIKit.CreateText(font, todoContent, "Todo Empty", "Nothing on the to-do list yet.", size,
                                                   TextAlignmentOptions.TopLeft, FontStyles.Italic, textColor);
            RectTransform er = empty.rectTransform;
            er.anchorMin = er.anchorMax = er.pivot = new Vector2(0f, 1f);
            er.sizeDelta = new Vector2(width, size * 1.5f);
            er.anchoredPosition = Vector2.zero;
            todoLines.Add(empty.gameObject);
            y = size * 1.5f;
        }
        PixelUIKit.UpdateScrollView(todoScroll, todoBar, y, todoViewRect.rect.height);
    }

    private void Close()
    {
        PixelDevHub.Close();
    }

    /// <summary>Gives a pet of every pixel type, quietly (no popups).</summary>
    /// <summary>Gives the pet of the pixel selected in the top drop-down (a popup), or every pet quietly for "All pixels".</summary>
    private void GivePet()
    {
        PixelPets pets = PixelPets.Instance;
        if (pets == null || clicker == null || pixelDropdown == null) return;
        int selected = PickedPixelTier; // -1 = All pixels
        if (selected < 0)
        {
            foreach (PixelClicker.PixelTier t in clicker.Tiers) pets.Award(t.type, false, true);   // switched off, so they can be turned on one by one in Toggles > Pets
            return;
        }
        if (selected < clicker.Tiers.Length) pets.Award(clicker.Tiers[selected].type, true, true);
    }

    private void FillDropdowns()
    {
        PixelClicker.PixelTier[] tiers = clicker.Tiers;
        int keep = pixelDropdown.value;
        List<string> names = new List<string>(), keys = new List<string>();
        names.Add(allPixelsText); keys.Add("");
        foreach (PixelClicker.PixelTier t in tiers) { names.Add(t.unlocked ? t.displayName : t.displayName + " (locked)"); keys.Add(t.displayName); }
        pixelMap = PixelUIKit.AddSortedOptions(pixelDropdown, names, 1, keys);   // (alphabetical; 'All pixels' stays first)
        pixelDropdown.SetValueWithoutNotify(Mathf.Clamp(keep, 0, Mathf.Max(0, names.Count - 1)));
        pixelDropdown.RefreshShownValue();

    }

    private void BuildPanel()
    {
        // The Main tab of the Dev Hub (PixelDevHub): everything below lives in its content root.
        PixelDevHub.Boot();
        canvasRoot = PixelDevHub.CanvasObject;
        RectTransform root = PixelDevHub.TabRoot("main");
        BuildTipBox();

        float y = 8f;
        float inner = PixelDevHub.ContentW - 100f; // usable width of a row (40 px left, 60 right: room for the scroll bar)

        GameObject tools = new GameObject("Tools View", typeof(RectTransform));
        tools.transform.SetParent(root, false);
        PixelUIKit.Stretch(tools.GetComponent<RectTransform>());

        // ONE widget board: every widget (add pixels, spawned pixel, minigames, middle click and the tools below) is a card in the same scrolling list. Drag a card by its header to
        // move it; HOLD the right button on a card to make it a FAVORITE (it snaps to the top and is tinted gold; hold again to let it go back).
        ScrollRect toolsScroll = PixelUIKit.CreateScrollView(tools.transform, "Tools Scroll", new Color(0.5f, 0.5f, 0.6f, 1f), 16f, 60f,
                                                             out RectTransform content, out GameObject toolsBar);
        RectTransform scrollRect = toolsScroll.GetComponent<RectTransform>();
        scrollRect.anchorMin = Vector2.zero;
        scrollRect.anchorMax = Vector2.one;
        scrollRect.offsetMin = new Vector2(0f, 8f);
        scrollRect.offsetMax = new Vector2(0f, -52f);   // (room above for the collapse-all bar)
        y = 0f; // rows are placed from the top of the scrolling content (each one becomes a draggable widget card)
        PixelWidgetBoard board = new PixelWidgetBoard(content, PrefPrefix + "WidgetOrder2", 8f, height => PixelUIKit.UpdateScrollView(toolsScroll, toolsBar, height, PixelDevHub.ContentH - 16f), toolsScroll.viewport);
        board.SearchTab = "main";   // (its cards show up in the hub's search bar)

        // 'Collapse all' above the widgets: folds every card up to its header (and back: it reads 'Expand all' while everything is folded).
        Button collapse = PixelUIKit.CreateButton(font, tools.transform, "Collapse All Button", "Collapse all", new Vector2(220f, 36f), new Color(0.3f, 0.36f, 0.5f, 1f), textColor, 22f);
        RectTransform collapseRect = collapse.GetComponent<RectTransform>();
        collapseRect.anchorMin = collapseRect.anchorMax = collapseRect.pivot = new Vector2(0f, 1f);
        collapseRect.sizeDelta = new Vector2(220f, 36f);
        collapseRect.anchoredPosition = new Vector2(10f, -8f);
        TMP_Text collapseLabel = collapse.GetComponentInChildren<TMP_Text>();
        System.Action refreshCollapse = () => { collapseLabel.text = board.AllMinimized ? "Expand all" : "Collapse all"; };
        collapse.onClick.AddListener(() => board.SetAllMinimized(!board.AllMinimized));
        board.MinimizeChanged = refreshCollapse;

        // Widget: pixel dropdown | amount field | Add | Give pet
        int pinStart = content.childCount;
        float fieldW = inner * 0.30f, dropW = inner - fieldW - rowHeight * 2f - 30f;
        pixelDropdown = PixelUIKit.CreateDropdown(font, content, "Pixel Dropdown", new Vector2(dropW, rowHeight),
                                                  boxColor, listColor, textColor, fontSize);
        Place(pixelDropdown.GetComponent<RectTransform>(), 40f, y, dropW);
        amountField = PixelUIKit.CreateInputField(font, content, "Amount Field", new Vector2(fieldW, rowHeight),
                                                  boxColor, textColor, fontSize, "amount");
        amountField.text = defaultAmount;
        amountField.onValueChanged.AddListener(text => { if (rememberSettings) PixelPrefs.SetString(PrefPrefix + "Amount", text); });
        Place(amountField.GetComponent<RectTransform>(), 40f + dropW + 10f, y, fieldW);
        Button add = IconButton(content, "Add Button", DevIcons.PlusSprite(), new Color(0.3f, 0.78f, 0.38f, 1f), rowHeight);
        Place(add.GetComponent<RectTransform>(), 40f + dropW + fieldW + 20f, y, rowHeight);
        add.onClick.AddListener(AddSelected);
        AddTip(add.gameObject, addTipText);
        Button pet = IconButton(content, "Give Pet Button", DevIcons.PawSprite(), new Color(0.97f, 0.82f, 0.22f, 1f), rowHeight);
        Place(pet.GetComponent<RectTransform>(), 40f + dropW + fieldW + 30f + rowHeight, y, rowHeight);
        pet.onClick.AddListener(GivePet);
        AddTip(pet.gameObject, petTipText);
        Widget(content, board, "addpixel", pinStart, y);

        BuildSpawnWidgets(content, board, inner, y);
        int ws = content.childCount;

        // ---- Dev reports: every report / snapshot tool in one card, two buttons per row. ----
        {
            float half = (inner - 10f) * 0.5f, x2 = 40f + half + 10f, y2 = y + rowHeight + WidgetRowGap, y3 = y2 + rowHeight + WidgetRowGap;

            // Row 1: crash report folder | snapshot bounds (waits so the panel can be closed first)
            Button report = PixelUIKit.CreateButton(font, content, "Report Folder Button", reportsCrashLabel, new Vector2(half, rowHeight), buttonColor, textColor, fontSize);
            Place(report.GetComponent<RectTransform>(), 40f, y, half);
            report.onClick.AddListener(PixelCrashLog.OpenFolder);
            report.interactable = PixelCrashLog.Available;   // (crash reports switched off: the button stays, greyed out)
            AddTip(report.gameObject, reportTipText);
            Button snap = PixelUIKit.CreateButton(font, content, "Bounds Snapshot Button", reportsBoundsLabel, new Vector2(half, rowHeight), buttonColor, textColor, fontSize);
            Place(snap.GetComponent<RectTransform>(), x2, y, half);
            snap.onClick.AddListener(() => StartCoroutine(ProbeRoutine(() => PixelBoundsSnapshot.Capture(), "Bounds snapshot")));

            // Row 2: mark this moment | UI layout dump (same wait; the keys F10 / F11 need no wait)
            Button moment = PixelUIKit.CreateButton(font, content, "Mark Moment Button", reportsMomentLabel, new Vector2(half, rowHeight), buttonColor, textColor, fontSize);
            Place(moment.GetComponent<RectTransform>(), 40f, y2, half);
            moment.onClick.AddListener(() => StartCoroutine(ProbeRoutine(() => PixelDevProbe.MarkMoment(), "Marked moment")));
            Button uiDump = PixelUIKit.CreateButton(font, content, "UI Layout Button", reportsUiLabel, new Vector2(half, rowHeight), buttonColor, textColor, fontSize);
            Place(uiDump.GetComponent<RectTransform>(), x2, y2, half);
            uiDump.onClick.AddListener(() => StartCoroutine(ProbeRoutine(() => PixelDevProbe.UiLayoutDump(), "UI layout dump")));

            // Row 3: skill balance report (no wait) | session recorder (Start, then Stop & save)
            Button balance = PixelUIKit.CreateButton(font, content, "Balance Report Button", reportsBalanceLabel, new Vector2(half, rowHeight), buttonColor, textColor, fontSize);
            Place(balance.GetComponent<RectTransform>(), 40f, y3, half);
            balance.onClick.AddListener(() => PixelBalanceReport.Capture(balanceManualCps, balanceAutoCps, (int[])balanceLevels.Clone()));
            Button session = PixelUIKit.CreateButton(font, content, "Session Recorder Button", sessionStartLabel, new Vector2(half, rowHeight), buttonColor, textColor, fontSize);
            Place(session.GetComponent<RectTransform>(), x2, y3, half);
            sessionLabel = session.GetComponentInChildren<TMP_Text>();
            session.onClick.AddListener(() =>
            {
                if (PixelSessionRecorder.Recording) PixelSessionRecorder.StopAndSave();
                else PixelSessionRecorder.StartRecording();
                RefreshSessionLabel();
            });
            AddTip(session.gameObject, "Records a real play session (clicking speed, income per minute, milestones, husks) and writes a report to paste to Claude. Only time in the game counts.");
            AddTip(balance.gameObject, "Balance report: XP and pay per click for every skill at the reference clicking speeds, saved and copied to the clipboard.");
            AddTip(moment.gameObject, "Mark this moment: a screenshot + a report of the game right now (waits 3 s so you can close this window; the F10 key does it at once).");
            AddTip(uiDump.gameObject, "UI layout dump: every open UI element as text - positions, sizes, texts (waits 3 s).");
            AddTip(snap.gameObject, "Snapshot bounds: the camera / floor / view-bounds numbers, for tuning where husks can go (waits 3 s).");
            Widget(content, board, "devreports", ws, y, 3);
            y += rowHeight + 24f;
            ws = content.childCount;
        }

        // Row: reset every pixel count (inventory, Log, Bank) but keep the unlocks - needs a second click to confirm
        resetButton = PixelUIKit.CreateButton(font, content, "Reset Counts Button", resetCountsText,
                                              new Vector2(inner, rowHeight), buttonColor, textColor, fontSize);
        Place(resetButton.GetComponent<RectTransform>(), 40f, y, inner);
        resetLabel = resetButton.GetComponentInChildren<TMP_Text>();
        resetButton.onClick.AddListener(ResetCountsClicked);
        Widget(content, board, "reset", ws, y);
        y += rowHeight + 24f;
        ws = content.childCount;

        // Row: tick box for the clear key
        clearToggle = BuildToggleRow(content, clearToggleText, y, inner, clearKeyEnabled, on => { clearKeyEnabled = on; SetBool("ClearKey", on); });
        Widget(content, board, "clear", ws, y);
        y += rowHeight + 24f;
        ws = content.childCount;

        // Row: tick box for infinite resources
        infiniteToggle = BuildToggleRow(content, infiniteToggleText, y, inner, infiniteResources,
                       on => { infiniteResources = on; PixelClicker.InfiniteResources = on; SetBool("Infinite", on); });
        Widget(content, board, "infinite", ws, y);
        y += rowHeight + 24f;
        ws = content.childCount;


        // Row: tick box to stop old pixels despawning
        noDespawnToggle = BuildToggleRow(content, noDespawnToggleText, y, inner, disableDespawn, on =>
        {
            disableDespawn = on;
            OldPixelDespawn.DevNoDespawn = on;
            SetBool("NoDespawn", on);
        });
        Widget(content, board, "nodespawn", ws, y);
        y += rowHeight + 24f;
        ws = content.childCount;

        // Row: tick box to remove the old pixel limit
        noLimitToggle = BuildToggleRow(content, noLimitToggleText, y, inner, disableOldPixelLimit, on =>
        {
            disableOldPixelLimit = on;
            PixelClicker.DevNoOldPixelLimit = on;
            SetBool("NoLimit", on);
        });
        Widget(content, board, "nolimit", ws, y);
        y += rowHeight + 24f;
        ws = content.childCount;

        // Row: tick box that makes the floor circle twice as big
        doubleFloorToggle = BuildToggleRow(content, doubleFloorToggleText, y, inner, doubleFloor, on =>
        {
            doubleFloor = on;
            PixelFloor.DoubleArea = on;
            SetBool("DoubleFloor", on);
        });
        Widget(content, board, "doublefloor", ws, y);
        y += rowHeight + 24f;
        ws = content.childCount;

        // Row: tick box for god pixel mode
        godToggle = BuildToggleRow(content, godModeLabel, y, inner, godMode, SetGodMode, godTipText);
        Widget(content, board, "god", ws, y);
        y += rowHeight + 24f;
        ws = content.childCount;



        // Row: how many clicks one click counts as (payouts, tough-pixel hits and the combo all scale with it)
        TMP_Text clickLabel = PixelUIKit.CreateText(font, content, "Click Count Label", clickCountText, fontSize,
                                                    TextAlignmentOptions.MidlineLeft, FontStyles.Normal, textColor);
        Place(clickLabel.rectTransform, 40f, y, inner * 0.6f - 10f);
        clickCountField = PixelUIKit.CreateInputField(font, content, "Click Count Field", new Vector2(inner * 0.4f, rowHeight),
                                                      boxColor, textColor, fontSize, "1");
        clickCountField.contentType = TMP_InputField.ContentType.IntegerNumber;
        clickCountField.SetTextWithoutNotify(clickCount.ToString());
        clickCountField.onValueChanged.AddListener(text =>
        {
            int.TryParse(text.Trim(), out int parsed);
            clickCount = Mathf.Clamp(parsed, 1, 1000000); // empty / invalid / 0 = a normal click
            PixelClicker.DevClickCount = clickCount;
            SetInt("ClickCount", clickCount);
        });
        Place(clickCountField.GetComponent<RectTransform>(), 40f + inner * 0.6f, y, inner * 0.4f);
        Widget(content, board, "clicks", ws, y);
        y += rowHeight + 24f;

        BuildResourceToolsCard(content, board);
        board.Finish();   // the saved order, the layout and the scroll size
        refreshCollapse();

        // To-Do and Changelog are the first two sub-tabs of the hub's Docs tab (PixelDevDocs); they are rebuilt each time one is shown.
        BuildTodoView(PixelDevDocs.Root(0), 8f, inner);
        BuildChangelogView(PixelDevDocs.Root(1), 8f);
        PixelDevDocs.Shown += OnDocsShown;

        FillDropdowns();
        pixelDropdown.SetValueWithoutNotify(Mathf.Clamp(GetInt("PixelIndex", 0), 0, pixelDropdown.options.Count - 1));
        pixelDropdown.RefreshShownValue();
        pixelDropdown.onValueChanged.AddListener(v => SetInt("PixelIndex", v));
        FillSelector();
        if (spawnSelectorEnabled) ApplySelector(selectorDropdown.value);
        RefreshSpawnedPixelButton();
        if (godMode) SetGodMode(true);
        // Saved shapes / mockup pieces come back even while the hub is closed.
        PixelDevShapes.Boot();
        PixelDevMockup.Boot();
        PixelDevHub.OpenChanged += OnHubOpenChanged;
        if (PixelDevHub.IsOpen) FillDropdowns();
    }

    private void OnDocsShown(int sub)
    {
        if (sub == 0) RebuildTodo();
        else if (sub == 1) RebuildChangelog();
    }

    private void OnHubOpenChanged(bool open)
    {
        if (open && pixelDropdown != null) { FillDropdowns(); FillSelector(); }
    }

    /// <summary>Turns the controls built since child number 'start' of 'content' (one row) into a draggable widget card at row position y.</summary>
    private static string WidgetTitle(string id)
    {
        switch (id)
        {
            case "resources": return "Resource Tools";
            case "addpixel": return "Add pixels";
            case "spawnpixel": return "Spawned pixel";
            case "spawnminigame": return "Minigames";
            case "middleclick": return "Middle click";
            case "focus": return "Focus potions";
            case "shopunlock": return "Unlock";
            case "maxxp": return "Max XP";
            case "huskpile": return "Husk pile";
            case "devreports": return "Dev reports";
            case "superrares": return "Super Rares";
            case "devitems": return "Dev items";
            case "reset": return "Reset counts";
            case "clear": return "Clear key";
            case "infinite": return "Infinite resources";
            case "nodespawn": return "Old pixel despawns";
            case "nolimit": return "Old pixel limit";
            case "god": return "God mode";
            case "clicks": return "Click count";
            default: return id;
        }
    }

    /// <summary>The 'Fill floor' button: random unlocked husks dropped in a ring round the cube, a few per frame, until the floor limit is reached.</summary>
    private System.Collections.IEnumerator FillFloorWithHusks()
    {
        PixelClicker c = PixelFind.Cached<PixelClicker>();
        if (c == null || c.PixelTransform == null) yield break;
        List<int> pool = new List<int>();
        for (int i = 0; i < c.Tiers.Length; i++)
            if (c.Tiers[i] != null && c.Tiers[i].unlocked && !c.Tiers[i].flyAway) pool.Add(i);
        if (pool.Count == 0) yield break;
        float unit = c.PixelBaseSize;
        int guard = 1000;
        while (c.OldPixels.Count < c.OldPixelCap && guard-- > 0)
        {
            for (int k = 0; k < fillFloorPerFrame && c.OldPixels.Count < c.OldPixelCap; k++)
            {
                Vector2 ring = Random.insideUnitCircle.normalized * Random.Range(2f, 6f) * unit;
                Vector3 at = c.PixelTransform.position + new Vector3(ring.x, Random.Range(0f, 1f) * unit, ring.y);
                int tier = pool[Random.Range(0, pool.Count)];
                c.SpawnStoredPixel(tier, c.Tiers[tier].amountPerClick, at, Random.insideUnitSphere * 0.5f);
            }
            yield return null;
        }
    }

    // ---- Resource Tools: these tools share ONE card (in this order) instead of a card each; ids with a label of their own get no sub-title. ----
    private static readonly string[] ResourceToolIds = { "infinite", "god", "nodespawn", "huskpile", "clear", "nolimit", "spawnpixel", "middleclick", "addpixel", "focus", "clicks" };
    private static readonly HashSet<string> ResourceToolsWithOwnLabel = new HashSet<string> { "infinite", "god", "nodespawn", "clear", "nolimit", "clicks" };
    private readonly Dictionary<string, KeyValuePair<List<Transform>, float>> resourceParts = new Dictionary<string, KeyValuePair<List<Transform>, float>>();
    private const float ResourceSubtitleH = 26f;

    /// <summary>Builds the 'Resource Tools' card from the tools Widget set aside (ResourceToolIds), stacked in that order with a small title over the ones without a label.</summary>
    private void BuildResourceToolsCard(RectTransform content, PixelWidgetBoard board)
    {
        float hdr = PixelWidgetBoard.CardHeaderH, inner = PixelDevHub.ContentW - 100f;
        GameObject card = new GameObject("Widget resources", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(content, false);
        RectTransform cr = card.GetComponent<RectTransform>();
        cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(0f, 1f);
        Image im = card.GetComponent<Image>();
        im.color = new Color(1f, 1f, 1f, 0.05f);
        im.raycastTarget = false;
        PixelUIKit.StyleBox(im);
        float acc = 0f;   // how far down the card the next tool starts (below the header strip)
        foreach (string id in ResourceToolIds)
        {
            if (!resourceParts.TryGetValue(id, out KeyValuePair<List<Transform>, float> part)) continue;
            if (!ResourceToolsWithOwnLabel.Contains(id))
            {
                TMP_Text sub = PixelUIKit.CreateText(font, card.transform, id + " Title", WidgetTitle(id), fontSize - 3f, TextAlignmentOptions.BottomLeft, FontStyles.Bold, new Color(0.6f, 0.75f, 1f, 1f));
                sub.raycastTarget = false;
                RectTransform sr = sub.rectTransform;
                sr.anchorMin = sr.anchorMax = sr.pivot = new Vector2(0f, 1f);
                sr.sizeDelta = new Vector2(inner, ResourceSubtitleH);
                sr.anchoredPosition = new Vector2(40f, -(8f + hdr + acc));
                acc += ResourceSubtitleH;
            }
            foreach (Transform k in part.Key)
            {
                k.SetParent(card.transform, false);
                RectTransform kr = (RectTransform)k;
                kr.anchoredPosition += new Vector2(0f, part.Value - 8f - hdr - acc);   // (built at row -y; now at this tool's place in the card)
                foreach (TMP_Dropdown dd in k.GetComponentsInChildren<TMP_Dropdown>(true)) PixelUIKit.LetListEscapeMasks(dd);
                foreach (Button b in k.GetComponentsInChildren<Button>(true)) PixelUIKit.FitOneLine(b.GetComponentInChildren<TMP_Text>(true));
            }
            acc += rowHeight + WidgetRowGap + 4f;
        }
        float cardH = hdr + 8f + acc + 4f;
        cr.sizeDelta = new Vector2(PixelDevHub.ContentW - 24f, cardH);
        board.Add("resources", cr, cardH, WidgetTitle("resources"));
    }

    private void Widget(RectTransform content, PixelWidgetBoard board, string id, int start, float y, int rows = 1)
    {
        List<Transform> kids = new List<Transform>();
        for (int i = start; i < content.childCount; i++) kids.Add(content.GetChild(i));
        if (System.Array.IndexOf(ResourceToolIds, id) >= 0) { resourceParts[id] = new KeyValuePair<List<Transform>, float>(kids, y); return; }   // (goes into the Resource Tools card)
        float hdr = PixelWidgetBoard.CardHeaderH;   // the card's header strip (title + minimize button) sits above the controls
        float cardH = rows * rowHeight + (rows - 1) * WidgetRowGap + 16f + hdr;   // (a widget with more rows places them WidgetRowGap apart)
        GameObject card = new GameObject("Widget " + id, typeof(RectTransform), typeof(Image));
        card.transform.SetParent(content, false);
        RectTransform cr = card.GetComponent<RectTransform>();
        cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(0f, 1f);
        cr.sizeDelta = new Vector2(PixelDevHub.ContentW - 24f, cardH);
        cr.anchoredPosition = new Vector2(0f, -(y - 8f));
        Image im = card.GetComponent<Image>();
        im.color = new Color(1f, 1f, 1f, 0.05f);
        im.raycastTarget = false;
        PixelUIKit.StyleBox(im);
        foreach (Transform k in kids)
        {
            k.SetParent(card.transform, false);
            RectTransform kr = (RectTransform)k;
            kr.anchoredPosition += new Vector2(0f, y - 8f - hdr);   // (they were placed at -y; the card starts 8 above that, and its header strip takes the top)
            foreach (TMP_Dropdown dd in k.GetComponentsInChildren<TMP_Dropdown>(true)) PixelUIKit.LetListEscapeMasks(dd);   // (an opened drop-down list must not be clipped by the scrolling board)
            foreach (Button b in k.GetComponentsInChildren<Button>(true)) PixelUIKit.FitOneLine(b.GetComponentInChildren<TMP_Text>(true));   // (long labels such as DESPAWN shrink to fit their button)
        }
        board.Add(id, cr, cardH, WidgetTitle(id));
    }

    // ------------------------------------------------------------------
    // On-screen spawn selector
    // ------------------------------------------------------------------

    private Button resetButton;
    private TMP_Text resetLabel;
    private float resetConfirmUntil;

    private void ResetCountsClicked()
    {
        if (clicker == null) return;
        if (Time.unscaledTime > resetConfirmUntil)
        {
            resetConfirmUntil = Time.unscaledTime + 4f; // first click: ask again
            if (resetLabel != null) resetLabel.text = resetConfirmText;
            return;
        }

        resetConfirmUntil = 0f;
        clicker.ResetAllCounts();
        PixelBank bank = PixelFind.First<PixelBank>();
        if (bank != null) bank.SetState(null); // the stored pixels too
        if (resetLabel != null) resetLabel.text = resetCountsText;
    }

    // ------------------------------------------------------------------
    // Spawn widgets (they replaced the on-screen row at the top of the screen): 'Spawned pixel', 'Minigames' and 'Middle click'.
    // They sit on the pinned widget board at the top of the Main tab (a drop-down's list would be clipped inside the scrolling board).
    // ------------------------------------------------------------------

    private TMP_Dropdown selectorDropdown, selectorMinigameDropdown;
    private readonly List<PixelClicker.PixelType[]> selectorChoices = new List<PixelClicker.PixelType[]>();
    private Image spawnPixelButtonImage;

    /// <summary>Builds the three spawn widgets into the pinned board (each is one row of controls on a card; 'y' is the row position the controls are placed at).</summary>
    private void BuildSpawnWidgets(RectTransform root, PixelWidgetBoard board, float inner, float y)
    {
        float bw = 140f, gap = 10f, dropW = inner - bw * 2f - gap * 2f;

        // ---- Spawned pixel: which pixel the cube spawns. Spawn = make only this one spawn (and swap the current block now), Despawn = back to normal. ----
        int start = root.childCount;
        selectorDropdown = PixelUIKit.CreateDropdown(font, root, "Spawned Pixel Dropdown", new Vector2(dropW, rowHeight), boxColor, listColor, textColor, fontSize);
        Place(selectorDropdown.GetComponent<RectTransform>(), 40f, y, dropW);
        selectorDropdown.onValueChanged.AddListener(v => SetInt("SelPixel", v));
        Button spawn = PixelUIKit.CreateButton(font, root, "Spawn Pixel Button", spawnText, new Vector2(bw, rowHeight), buttonColor, textColor, fontSize);
        Place(spawn.GetComponent<RectTransform>(), 40f + dropW + gap, y, bw);
        spawn.onClick.AddListener(SpawnPixelClicked);
        spawnPixelButtonImage = spawn.GetComponent<Image>();
        Button despawn = PixelUIKit.CreateButton(font, root, "Despawn Pixel Button", despawnText, new Vector2(bw, rowHeight), buttonColor, textColor, fontSize);
        Place(despawn.GetComponent<RectTransform>(), 40f + dropW + gap + bw + gap, y, bw);
        despawn.onClick.AddListener(DespawnPixelClicked);
        AddTip(spawn.gameObject, "Make only the chosen pixel (or the Monochrome / RGB pack) spawn on the cube, and swap the pixel on the cube to it now.");
        AddTip(despawn.gameObject, "Go back to normal spawning.");
        Widget(root, board, "spawnpixel", start, y);

        // ---- Minigames: pick one, Spawn it or Despawn it (Despawn clears whatever minigame is on screen when the chosen one isn't). ----
        start = root.childCount;
        selectorMinigameDropdown = PixelUIKit.CreateDropdown(font, root, "Minigame Dropdown", new Vector2(dropW, rowHeight), boxColor, listColor, textColor, fontSize);
        Place(selectorMinigameDropdown.GetComponent<RectTransform>(), 40f, y, dropW);
        selectorMinigameDropdown.onValueChanged.AddListener(v => SetInt("SelMinigame", v));
        Button mgSpawn = PixelUIKit.CreateButton(font, root, "Spawn Minigame Button", spawnText, new Vector2(bw, rowHeight), buttonColor, textColor, fontSize);
        Place(mgSpawn.GetComponent<RectTransform>(), 40f + dropW + gap, y, bw);
        mgSpawn.onClick.AddListener(SpawnSelected);
        Button mgDespawn = PixelUIKit.CreateButton(font, root, "Despawn Minigame Button", despawnText, new Vector2(bw, rowHeight), buttonColor, textColor, fontSize);
        Place(mgDespawn.GetComponent<RectTransform>(), 40f + dropW + gap + bw + gap, y, bw);
        mgDespawn.onClick.AddListener(DespawnSelected);
        Widget(root, board, "spawnminigame", start, y);

        // ---- Middle click: swap the pixel on the cube for the chosen one (first entry = off). ----
        start = root.childCount;
        swapDropdown = PixelUIKit.CreateDropdown(font, root, "Swap Dropdown", new Vector2(inner, rowHeight), boxColor, listColor, textColor, fontSize);
        Place(swapDropdown.GetComponent<RectTransform>(), 40f, y, inner);
        swapDropdown.onValueChanged.AddListener(v => SetInt("SelSwap", v));
        Widget(root, board, "middleclick", start, y);

        // ---- Focus potions: pick one, Drink it (60 s, replaces the active one) or Cancel the active one (PixelFocus). ----
        start = root.childCount;
        focusDropdown = PixelUIKit.CreateDropdown(font, root, "Focus Dropdown", new Vector2(dropW, rowHeight), boxColor, listColor, textColor, fontSize);
        Place(focusDropdown.GetComponent<RectTransform>(), 40f, y, dropW);
        focusDropdown.onValueChanged.AddListener(v => SetInt("SelFocus", v));
        Button drink = PixelUIKit.CreateButton(font, root, "Drink Focus Button", drinkFocusText, new Vector2(bw, rowHeight), buttonColor, textColor, fontSize);
        Place(drink.GetComponent<RectTransform>(), 40f + dropW + gap, y, bw);
        drink.onClick.AddListener(DrinkFocus);
        Button cancelFocus = PixelUIKit.CreateButton(font, root, "Cancel Focus Button", cancelFocusText, new Vector2(bw, rowHeight), buttonColor, textColor, fontSize);
        Place(cancelFocus.GetComponent<RectTransform>(), 40f + dropW + gap + bw + gap, y, bw);
        cancelFocus.onClick.AddListener(CancelFocus);
        AddTip(drink.gameObject, "Drink the chosen focus potion: the skill's hazard block stops spawning while it lasts (replaces the one that is active).");
        AddTip(cancelFocus.gameObject, "Stop the active focus potion early.");
        Widget(root, board, "focus", start, y);

        // ---- Shop unlocks: pick a shop pack (or All), Unlock it for free or Relock it. Shop packs only; 'Unlock everything' also unlocks pixels and maxes skills. ----
        start = root.childCount;
        shopUnlockDropdown = PixelUIKit.CreateDropdown(font, root, "Shop Unlock Dropdown", new Vector2(dropW, rowHeight), boxColor, listColor, textColor, fontSize);
        Place(shopUnlockDropdown.GetComponent<RectTransform>(), 40f, y, dropW);
        shopUnlockDropdown.onValueChanged.AddListener(v => SetInt("SelShopPack", v));
        Button shopUnlock = PixelUIKit.CreateButton(font, root, "Shop Unlock Button", shopUnlockText, new Vector2(bw, rowHeight), buttonColor, textColor, fontSize);
        Place(shopUnlock.GetComponent<RectTransform>(), 40f + dropW + gap, y, bw);
        shopUnlock.onClick.AddListener(() => ShopUnlockPicked(true));
        Button shopRelock = PixelUIKit.CreateButton(font, root, "Shop Relock Button", shopRelockText, new Vector2(bw, rowHeight), buttonColor, textColor, fontSize);
        Place(shopRelock.GetComponent<RectTransform>(), 40f + dropW + gap + bw + gap, y, bw);
        shopRelock.onClick.AddListener(() => ShopUnlockPicked(false));
        AddTip(shopUnlock.gameObject, "Unlock the picked shop pack for free (an upgrade goes to its top level). 'All shop items' unlocks every pack and every pixel (their switches are left off). Skills are not touched: use Max XP.");
        AddTip(shopRelock.gameObject, "Take the picked shop pack back: its pixels are locked again and its feature switched off (an upgrade goes to level 0). 'All' relocks every shop pack.");
        Widget(root, board, "shopunlock", start, y);

        // ---- Max XP: pick a skill (or All skills), give it the XP up to its level cap, or take its XP away. ----
        start = root.childCount;
        skillXpDropdown = PixelUIKit.CreateDropdown(font, root, "Skill XP Dropdown", new Vector2(dropW, rowHeight), boxColor, listColor, textColor, fontSize);
        Place(skillXpDropdown.GetComponent<RectTransform>(), 40f, y, dropW);
        skillXpDropdown.onValueChanged.AddListener(v => SetInt("SelSkillXp", v));
        Button maxXp = PixelUIKit.CreateButton(font, root, "Max XP Button", maxXpText, new Vector2(bw, rowHeight), buttonColor, textColor, fontSize);
        Place(maxXp.GetComponent<RectTransform>(), 40f + dropW + gap, y, bw);
        maxXp.onClick.AddListener(() => SkillXpPicked(true));
        Button resetXp = PixelUIKit.CreateButton(font, root, "Reset XP Button", resetXpText, new Vector2(bw, rowHeight), buttonColor, textColor, fontSize);
        Place(resetXp.GetComponent<RectTransform>(), 40f + dropW + gap + bw + gap, y, bw);
        resetXp.onClick.AddListener(() => SkillXpPicked(false));
        AddTip(maxXp.gameObject, "Give the picked skill (or every skill, Standard included) the XP up to its level cap. No popups.");
        AddTip(resetXp.gameObject, "Take all XP away from the picked skill (or every skill): back to level 1.");
        Widget(root, board, "maxxp", start, y);
        FillSkillXpDropdown();

        // ---- Husk pile: fill the floor up to the husk limit, to profile a full floor (Dev Inspector's World tab shows bodies / awake). ----
        start = root.childCount;
        Button fillFloor = PixelUIKit.CreateButton(font, root, "Fill Floor Button", fillFloorText, new Vector2(bw, rowHeight), buttonColor, textColor, fontSize);
        Place(fillFloor.GetComponent<RectTransform>(), 40f, y, bw);
        fillFloor.onClick.AddListener(() => StartCoroutine(FillFloorWithHusks()));
        AddTip(fillFloor.gameObject, "Drop random husks round the cube until the floor limit is reached. Profile with Window > Analysis > Profiler; the Dev Inspector's World tab shows how many bodies are awake.");
        Widget(root, board, "huskpile", start, y);

        // ---- Super Rares: pick a Dragon / God / Prime Cube, the Cubelette or the Golden Snitch and Spawn it on the clicker cube; Gauntlet gives a Prime Gauntlet. ----
        start = root.childCount;
        rareDropdown = PixelUIKit.CreateDropdown(font, root, "Super Rares Dropdown", new Vector2(dropW, rowHeight), boxColor, listColor, textColor, fontSize);
        Place(rareDropdown.GetComponent<RectTransform>(), 40f, y, dropW);
        rareDropdown.onValueChanged.AddListener(v => SetInt("SelRare", v));
        Button spawnRare = PixelUIKit.CreateButton(font, root, "Spawn Rare Button", spawnRareText, new Vector2(bw, rowHeight), buttonColor, textColor, fontSize);
        Place(spawnRare.GetComponent<RectTransform>(), 40f + dropW + gap, y, bw);
        spawnRare.onClick.AddListener(SpawnPickedRare);
        Button gauntlet = PixelUIKit.CreateButton(font, root, "Give Gauntlet Button", giveGauntletText, new Vector2(bw, rowHeight), buttonColor, textColor, fontSize);
        Place(gauntlet.GetComponent<RectTransform>(), 40f + dropW + gap + bw + gap, y, bw);
        gauntlet.onClick.AddListener(GiveGauntlet);
        AddTip(spawnRare.gameObject, "Replace the block on the clicker cube with the picked super rare (click it to collect it like a real find; the Golden Snitch waits for your click and flies).");
        AddTip(gauntlet.gameObject, "Put a Prime Gauntlet in your inventory (right-click it there to set the six Prime Cubes and forge the Cubelette).");
        // Second row: the Farmer always brings a Dragon Seed.
        farmerSeedsToggle = BuildToggleRow(root, farmerSeedsToggleText, y + rowHeight + WidgetRowGap, inner, farmerAlwaysSeeds, on =>
        {
            farmerAlwaysSeeds = on;
            PixelFarmerMinigame.DevAlwaysDragonSeeds = on;
            SetBool("FarmerSeeds", on);
        });
        Widget(root, board, "superrares", start, y, 2);
        FillRareDropdown();

        // ---- Dev items: the devices taken out of the shop (Device.devOnly) - pick one and Give puts one in your inventory. ----
        start = root.childCount;
        devItemDropdown = PixelUIKit.CreateDropdown(font, root, "Dev Items Dropdown", new Vector2(dropW, rowHeight), boxColor, listColor, textColor, fontSize);
        Place(devItemDropdown.GetComponent<RectTransform>(), 40f, y, dropW);
        Button giveItem = PixelUIKit.CreateButton(font, root, "Give Dev Item Button", giveDevItemText, new Vector2(bw, rowHeight), buttonColor, textColor, fontSize);
        Place(giveItem.GetComponent<RectTransform>(), 40f + dropW + gap, y, bw);
        giveItem.onClick.AddListener(GiveDevItem);
        AddTip(giveItem.gameObject, "Put one of the picked item in your inventory. These devices are not on sale anywhere for now (Quantum Organizer, Funnel, Sprinkler, Vacuum Device, Sorter, Bait Bucket, Conveyor Belt Kit).");
        Widget(root, board, "devitems", start, y);
        FillDevItemDropdown();
    }

    private const float WidgetRowGap = 10f;   // space between the rows of a multi-row widget card
    private TMP_Dropdown rareDropdown;
    private readonly List<PixelClicker.PixelType> rareTypes = new List<PixelClicker.PixelType>();   // dropdown option -> pixel type (in the game's own order)

    /// <summary>Fills the 'Super Rares' drop-down with every rare collectible tier: Dragon Cubes, God Cubes, Prime Cubes, the Cubelette, the Golden Snitch.</summary>
    private void FillRareDropdown()
    {
        if (rareDropdown == null || clicker == null) return;
        int keep = rareDropdown.options.Count > 0 ? rareDropdown.value : GetInt("SelRare", 0);
        rareTypes.Clear();
        List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>();
        foreach (PixelClicker.PixelTier t in clicker.Tiers)
        {
            if (t == null || !(t.rareDrop || PixelClicker.IsRareCube(t.type) || t.type == PixelClicker.PixelType.InfinityCubelette)) continue;
            if (rareTypes.Contains(t.type)) continue;
            rareTypes.Add(t.type);
            options.Add(new TMP_Dropdown.OptionData(t.displayName + (t.count >= 1d ? "  (held)" : "")));
        }
        rareDropdown.ClearOptions();
        rareDropdown.AddOptions(options);
        rareDropdown.SetValueWithoutNotify(Mathf.Clamp(keep, 0, Mathf.Max(0, options.Count - 1)));
        rareDropdown.RefreshShownValue();
    }

    /// <summary>Puts the picked super rare on the clicker cube in place of the current block.</summary>
    private void SpawnPickedRare()
    {
        if (clicker == null || rareDropdown == null) return;
        int v = rareDropdown.value;
        if (v < 0 || v >= rareTypes.Count) return;
        if (PixelGoldenSnitch.Active && rareTypes[v] == PixelClicker.PixelType.GoldenSnitch) return;   // (one snitch at a time)
        clicker.DevSwapCurrentPixel(rareTypes[v]);
        FillRareDropdown();
    }

    private TMP_Dropdown devItemDropdown;
    private readonly List<int> devItems = new List<int>();   // item index for each drop-down entry

    /// <summary>Fills the 'Dev items' drop-down with every Dev Tools-only device (PixelConsumables.ItemDevOnly).</summary>
    private void FillDevItemDropdown()
    {
        if (devItemDropdown == null) return;
        PixelConsumables consumables = PixelFind.Cached<PixelConsumables>();
        devItems.Clear();
        List<string> options = new List<string>();
        if (consumables != null)
            for (int i = 0; i < consumables.ItemCount; i++)
                if (consumables.ItemDevOnly(i)) { devItems.Add(i); options.Add(consumables.ItemName(i)); }
        devItemDropdown.ClearOptions();
        devItemDropdown.AddOptions(options);
        devItemDropdown.RefreshShownValue();
    }

    /// <summary>Gives one of the picked Dev Tools-only device.</summary>
    private void GiveDevItem()
    {
        PixelConsumables consumables = PixelFind.Cached<PixelConsumables>();
        if (consumables == null || devItemDropdown == null) return;
        if (devItems.Count == 0) FillDevItemDropdown();
        int v = devItemDropdown.value;
        if (v >= 0 && v < devItems.Count) consumables.AddItem(devItems[v], 1);
    }

    /// <summary>Gives one Prime Gauntlet (DeviceKind.Gauntlet) to the inventory.</summary>
    private void GiveGauntlet()
    {
        PixelConsumables consumables = PixelFind.First<PixelConsumables>();
        if (consumables == null) return;
        for (int i = 0; i < consumables.ItemCount; i++)
            if (consumables.IsDevice(i) && consumables.DeviceKindOf(i) == PixelConsumables.DeviceKind.Gauntlet) { consumables.AddItem(i, 1); return; }
    }

    private TMP_Dropdown skillXpDropdown;
    private int[] skillXpMap;   // option -> entry (0 = All skills, n = pack n - 1)

    /// <summary>Fills the 'Max XP' drop-down: 'All skills', then every skill with its level (alphabetical).</summary>
    private void FillSkillXpDropdown()
    {
        if (skillXpDropdown == null) return;
        PixelSkills s = PixelSkills.Instance;
        int keep = skillXpDropdown.options.Count > 0 ? skillXpDropdown.value : GetInt("SelSkillXp", 0);
        List<string> names = new List<string> { allSkillsText }, keys = new List<string> { "" };
        if (s != null && s.Packs != null)
            foreach (PixelSkills.SkillPack p in s.Packs)
            {
                names.Add(p != null ? p.displayName + "  (lv " + s.LevelOf(p) + ")" : "-");
                keys.Add(p != null ? p.displayName : "~");
            }
        skillXpMap = PixelUIKit.AddSortedOptions(skillXpDropdown, names, 1, keys);
        skillXpDropdown.SetValueWithoutNotify(Mathf.Clamp(keep, 0, Mathf.Max(0, names.Count - 1)));
        skillXpDropdown.RefreshShownValue();
    }

    private void SkillXpPicked(bool max)
    {
        PixelSkills s = PixelSkills.Instance;
        if (s == null || skillXpDropdown == null) return;
        int v = PixelUIKit.Original(skillXpMap, skillXpDropdown.value);
        if (v == 0) { foreach (PixelSkills.SkillPack p in s.Packs) s.DevSetSkillXp(p, max); }
        else if (v - 1 < s.Packs.Count) s.DevSetSkillXp(s.Packs[v - 1], max);
        FillSkillXpDropdown();
    }

    private TMP_Dropdown shopUnlockDropdown;
    private int[] shopUnlockMap;   // option -> entry (0 = All, n = pack n - 1)

    /// <summary>Unlock (true) or relock (false) the shop pack picked in the 'Shop unlocks' widget; entry 0 = every pack.</summary>
    private void ShopUnlockPicked(bool unlock)
    {
        if (shop == null || shopUnlockDropdown == null) return;
        int v = PixelUIKit.Original(shopUnlockMap, shopUnlockDropdown.value);
        if (v == 0) { if (unlock) shop.DevUnlockAll(); else shop.DevRelockAllPacks(); }   // ('All shop items' = every pack AND every pixel, switches left off)
        else if (unlock) shop.DevUnlockPack(v - 1);
        else shop.DevRelockPack(v - 1);
        FillShopUnlockDropdown(false);
    }

    /// <summary>Fills the 'Shop unlocks' drop-down: 'All', then every shop pack with its state (keeps the pick).</summary>
    private void FillShopUnlockDropdown(bool first)
    {
        if (shopUnlockDropdown == null) return;
        int keep = first ? GetInt("SelShopPack", 0) : shopUnlockDropdown.value;
        List<string> names = new List<string> { shopAllText }, keys = new List<string> { "" };
        if (shop != null && shop.AllPacks != null)
            for (int i = 0; i < shop.AllPacks.Count; i++)
            {
                PixelShop.ShopPack p = shop.AllPacks[i];
                if (p == null) { names.Add("-"); keys.Add("~"); continue; }
                string state = shop.IsPurchased(i) ? "  (unlocked)" : shop.GetPackLevel(i) > 0 ? "  (level " + shop.GetPackLevel(i) + ")" : "";
                names.Add(p.displayName + (p.hideInShop ? "  [not in shop]" : "") + state);
                keys.Add(p.displayName);
            }
        shopUnlockMap = PixelUIKit.AddSortedOptions(shopUnlockDropdown, names, 1, keys);   // (alphabetical; 'All' stays first)
        shopUnlockDropdown.SetValueWithoutNotify(Mathf.Clamp(keep, 0, Mathf.Max(0, names.Count - 1)));
        shopUnlockDropdown.RefreshShownValue();
    }

    private TMP_Dropdown focusDropdown;
    private int[] focusMap;   // option -> focus potion index

    private void DrinkFocus()
    {
        PixelFocus focus = PixelFocus.Instance;
        if (focus != null && focusDropdown != null) focus.Drink(PixelUIKit.Original(focusMap, focusDropdown.value));
    }

    private void CancelFocus()
    {
        if (PixelFocus.Instance != null) PixelFocus.Instance.Cancel();
    }

    /// <summary>Fills the focus potion drop-down from PixelFocus's list (keeps the pick).</summary>
    private void FillFocusDropdown(bool first)
    {
        if (focusDropdown == null) return;
        PixelFocus focus = PixelFocus.Instance;
        int keep = first ? GetInt("SelFocus", 0) : focusDropdown.value;
        List<string> focusNames = new List<string>();
        if (focus != null) foreach (PixelFocus.FocusPotion p in focus.Potions) focusNames.Add(p != null ? p.displayName : "-");
        focusMap = PixelUIKit.AddSortedOptions(focusDropdown, focusNames);   // (alphabetical)
        focusDropdown.SetValueWithoutNotify(Mathf.Clamp(keep, 0, Mathf.Max(0, focusNames.Count - 1)));
        focusDropdown.RefreshShownValue();
    }

    private void SpawnPixelClicked()
    {
        if (clicker == null || selectorDropdown == null) return;
        spawnSelectorEnabled = true;
        SetBool("Selector", true);
        int v = selectorDropdown.value;
        ApplySelector(v);
        if (v >= 0 && v < selectorChoices.Count && selectorChoices[v] != null && selectorChoices[v].Length == 1) clicker.DevSwapCurrentPixel(selectorChoices[v][0]);   // one exact pixel: the cube shows it right away
        RefreshSpawnedPixelButton();
    }

    private void DespawnPixelClicked()
    {
        if (clicker == null) return;
        spawnSelectorEnabled = false;
        SetBool("Selector", false);
        clicker.SetDevSpawnTiers(null);
        RefreshSpawnedPixelButton();
    }

    /// <summary>The Spawn button of the pixel widget is green while a spawn override is active.</summary>
    private void RefreshSpawnedPixelButton()
    {
        if (spawnPixelButtonImage != null) spawnPixelButtonImage.color = spawnSelectorEnabled ? new Color(0.25f, 0.6f, 0.3f, 1f) : buttonColor;
    }

    /// <summary>Pixel options: normal, Monochrome pack, RGB pack, then every pixel type (locked ones marked). Plus the minigame list and the middle-click list.</summary>
    private void FillSelector()
    {
        if (selectorDropdown == null || selectorMinigameDropdown == null || clicker == null) return;
        bool first = selectorChoices.Count == 0;
        int keep = first ? GetInt("SelPixel", 0) : selectorDropdown.value;
        selectorChoices.Clear();
        List<string> names = new List<string>();

        names.Add(spawnNormalText); selectorChoices.Add(null);
        names.Add(spawnMonochromeText);
        selectorChoices.Add(new[] { PixelClicker.PixelType.White, PixelClicker.PixelType.Gray, PixelClicker.PixelType.Black });
        names.Add(spawnRgbText);
        selectorChoices.Add(new[] { PixelClicker.PixelType.Red, PixelClicker.PixelType.Green, PixelClicker.PixelType.Blue });

        List<string> keys = new List<string> { "", "", "" };
        foreach (PixelClicker.PixelTier tier in clicker.Tiers)
        {
            names.Add(tier.displayName + (tier.unlocked ? "" : " (locked)"));
            keys.Add(tier.displayName);
            selectorChoices.Add(new[] { tier.type });
        }

        // Alphabetical (normal / Monochrome / RGB stay first); the choices are put in the same order as the options.
        int[] selMap = PixelUIKit.AddSortedOptions(selectorDropdown, names, 3, keys);
        List<PixelClicker.PixelType[]> selSorted = new List<PixelClicker.PixelType[]>();
        foreach (int i in selMap) selSorted.Add(selectorChoices[i]);
        selectorChoices.Clear();
        selectorChoices.AddRange(selSorted);
        selectorDropdown.SetValueWithoutNotify(Mathf.Clamp(keep, 0, names.Count - 1));
        selectorDropdown.RefreshShownValue();

        // Middle-click swap: off, then every pixel type.
        if (swapDropdown != null)
        {
            int keepSwap = first ? GetInt("SelSwap", 0) : swapDropdown.value;
            swapChoices.Clear();
            List<string> swapNames = new List<string> { swapPrefix + "off" }, swapKeys = new List<string> { "" };
            foreach (PixelClicker.PixelTier tier in clicker.Tiers)
            {
                swapNames.Add(swapPrefix + tier.displayName + (tier.unlocked ? "" : " (locked)"));
                swapKeys.Add(tier.displayName);
                swapChoices.Add(tier.type);
            }
            int[] swapMap = PixelUIKit.AddSortedOptions(swapDropdown, swapNames, 1, swapKeys);   // (alphabetical; 'off' stays first)
            List<PixelClicker.PixelType> swapSorted = new List<PixelClicker.PixelType>();
            for (int i = 1; i < swapMap.Length; i++) swapSorted.Add(swapChoices[swapMap[i] - 1]);
            swapChoices.Clear();
            swapChoices.AddRange(swapSorted);
            swapDropdown.SetValueWithoutNotify(Mathf.Clamp(keepSwap, 0, Mathf.Max(0, swapNames.Count - 1)));
            swapDropdown.RefreshShownValue();
        }

        // Minigames
        int keepGame = first ? GetInt("SelMinigame", 0) : selectorMinigameDropdown.value;
        dropdownMinigames.Clear();
        names.Clear();
        foreach (PixelMinigame game in PixelMinigame.All)
        {
            if (game == null) continue;
            dropdownMinigames.Add(game);
            names.Add(game.DisplayName);
        }
        int[] gameMap = PixelUIKit.AddSortedOptions(selectorMinigameDropdown, names);   // (alphabetical)
        List<PixelMinigame> gamesSorted = new List<PixelMinigame>();
        foreach (int i in gameMap) gamesSorted.Add(dropdownMinigames[i]);
        dropdownMinigames.Clear();
        dropdownMinigames.AddRange(gamesSorted);
        selectorMinigameDropdown.SetValueWithoutNotify(Mathf.Clamp(keepGame, 0, Mathf.Max(0, names.Count - 1)));
        selectorMinigameDropdown.RefreshShownValue();

        FillFocusDropdown(first);
        FillShopUnlockDropdown(first);
        FillSkillXpDropdown();
        FillRareDropdown();
    }

    private void ApplySelector(int index)
    {
        if (clicker == null || index < 0 || index >= selectorChoices.Count) return;
        clicker.SetDevSpawnTiers(selectorChoices[index]);
    }

    private void SpawnSelected()
    {
        if (selectorMinigameDropdown == null || selectorMinigameDropdown.value < 0 || selectorMinigameDropdown.value >= dropdownMinigames.Count) return;
        PixelMinigame game = dropdownMinigames[selectorMinigameDropdown.value];
        if (game != null) game.SpawnNow();
    }

    private void DespawnSelected()
    {
        if (selectorMinigameDropdown == null || selectorMinigameDropdown.value < 0 || selectorMinigameDropdown.value >= dropdownMinigames.Count) return;
        PixelMinigame game = dropdownMinigames[selectorMinigameDropdown.value];
        if (game != null && game.Busy) { game.DespawnNow(); return; }
        // The selected one isn't on screen: clear every minigame that is.
        foreach (PixelMinigame other in dropdownMinigames) if (other != null && other.Busy) other.DespawnNow();
    }

    // ------------------------------------------------------------------
    // God pixel mode: scroll = pick a pixel, left click = spawn, right click = destroy
    // ------------------------------------------------------------------

    private GameObject godRoot;
    private RectTransform godRect, godCanvasRect;
    private TMP_Text godLabel;
    private Image godSwatch;
    private readonly List<int> godTiers = new List<int>();
    private int godChoice;
    private float godSpawnTimer;

    private void SetGodMode(bool on)
    {
        godMode = on;
        SetBool("God", on);
        PixelClicker.GodMode = on;
        if (clicker == null) return;
        if (on)
        {
            if (godRoot == null) BuildGodDisplay();
            godChoice = Mathf.Clamp(GetInt("GodChoice", 0), 0, 999);
        }
        if (godRoot != null) godRoot.SetActive(on);
    }

    private void BuildGodDisplay()
    {
        godRoot = PixelUIKit.CreateCanvas("PixelDevTools God Pixel", 146, referenceResolution, false);
        godRoot.transform.SetParent(transform, false);
        godCanvasRect = godRoot.GetComponent<RectTransform>();

        GameObject holder = new GameObject("Display", typeof(RectTransform));
        holder.transform.SetParent(godRoot.transform, false);
        godRect = holder.GetComponent<RectTransform>();
        godRect.anchorMin = godRect.anchorMax = godRect.pivot = new Vector2(0f, 1f);
        godRect.sizeDelta = new Vector2(10f, 10f);

        GameObject sw = new GameObject("Swatch", typeof(RectTransform), typeof(Image));
        sw.transform.SetParent(holder.transform, false);
        godSwatch = sw.GetComponent<Image>();
        godSwatch.raycastTarget = false;
        RectTransform sr = sw.GetComponent<RectTransform>();
        sr.anchorMin = sr.anchorMax = sr.pivot = new Vector2(0f, 1f);
        sr.sizeDelta = new Vector2(godFontSize, godFontSize);
        sr.anchoredPosition = Vector2.zero;

        godLabel = PixelUIKit.CreateText(font, holder.transform, "Label", "", godFontSize, TextAlignmentOptions.MidlineLeft,
                                         FontStyles.Bold, textColor);
        godLabel.overflowMode = TextOverflowModes.Overflow;
        RectTransform lr = godLabel.rectTransform;
        lr.anchorMin = lr.anchorMax = lr.pivot = new Vector2(0f, 1f);
        lr.sizeDelta = new Vector2(400f, godFontSize * 1.2f);
        lr.anchoredPosition = new Vector2(godFontSize + 8f, 0f);
    }

    private void UpdateGod()
    {
        if (!godMode || clicker == null || godRoot == null) return;
        bool active = !PixelCalm.InMenu && !IsOpen;
        if (godRoot.activeSelf != active) godRoot.SetActive(active);
        if (!active) return;

        // The pixels that can be spawned (fly-away ones can't).
        godTiers.Clear();
        for (int i = 0; i < clicker.Tiers.Length; i++)
            if (!clicker.Tiers[i].flyAway) godTiers.Add(i);
        if (godTiers.Count == 0) return;
        godChoice = ((godChoice % godTiers.Count) + godTiers.Count) % godTiers.Count;

        Vector2 pointer = PixelInput.PointerPosition();
        bool overUI = PixelInput.PointerOverUI();

        // Scroll to choose.
        float scroll = overUI ? 0f : PixelInput.ScrollY();
        if (Mathf.Abs(scroll) > 0.01f)
        {
            godChoice += scroll > 0f ? -1 : 1;
            godChoice = ((godChoice % godTiers.Count) + godTiers.Count) % godTiers.Count;
            SetInt("GodChoice", godChoice);
            PixelAudio.Play("bank_select");
        }

        PixelClicker.PixelTier tier = clicker.Tiers[godTiers[godChoice]];

        // The little display next to the cursor.
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(godCanvasRect, pointer, null, out Vector2 local))
            godRect.anchoredPosition = local - new Vector2(godCanvasRect.rect.xMin, godCanvasRect.rect.yMax) + godDisplayOffset; // relative to the top-left anchor
        godSwatch.color = tier.UIColor;
        PixelUIKit.SetText(godLabel, tier.displayName);

        if (overUI || PixelBank.HoseOn) return;

        Camera cam = clicker.ViewCamera;
        if (cam == null) return;

        // Left: spawn old pixels at the cursor (hold to keep spawning).
        if (PixelInput.LeftPressed()) godSpawnTimer = 0f;
        if (PixelInput.LeftHeld())
        {
            godSpawnTimer -= Time.unscaledDeltaTime;
            if (godSpawnTimer <= 0f)
            {
                godSpawnTimer = 1f / godSpawnRate;
                Transform cube = clicker.PixelTransform;
                Vector3 anchor = cube != null ? cube.position : cam.transform.position + cam.transform.forward * 10f;
                Plane plane = new Plane(-cam.transform.forward, anchor);
                Ray ray = cam.ScreenPointToRay(pointer);
                Vector3 spawnAt = Vector3.zero;
                bool haveSpot = false;
                // Drop it above whatever the cursor is over (the floor, a hole), so it falls where it was pointed - not at the cube's depth.
                RaycastHit[] hits = Physics.RaycastAll(ray, 1000f, ~0, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                foreach (RaycastHit hit in hits)
                {
                    if (hit.collider.GetComponentInParent<OldPixelInfo>() != null) continue;
                    if (cube != null && hit.transform.IsChildOf(cube)) continue;
                    if (hit.normal.y < 0.5f) continue;
                    spawnAt = hit.point + Vector3.up * (clicker.PixelBaseSize * 2f);
                    haveSpot = true;
                    break;
                }
                if (!haveSpot && plane.Raycast(ray, out float enter)) { spawnAt = ray.GetPoint(enter); haveSpot = true; }
                if (haveSpot) clicker.SpawnStoredPixel(godTiers[godChoice], tier.amountPerClick, spawnAt, Random.insideUnitSphere * 0.3f);
            }
        }

        // Right: destroy old pixels near the cursor (no payout).
        if (PixelInput.RightHeld() || PixelInput.RightPressed())
        {
            var list = clicker.OldPixels;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                Rigidbody rb = list[i];
                if (rb == null) continue;
                Vector3 sp = cam.WorldToScreenPoint(rb.position);
                if (sp.z < 0f || ((Vector2)sp - pointer).sqrMagnitude > godEraseRadius * godEraseRadius) continue;
                clicker.ReleaseOldPixel(rb, false);
                Destroy(rb.gameObject);
            }
        }
    }

    /// <summary>Anchors a UI element to the panel's top-left corner at (x, y) with a width.</summary>
    private void Place(RectTransform rt, float x, float y, float width)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(width, rowHeight);
        rt.anchoredPosition = new Vector2(x, -y);
    }

    private Toggle BuildToggleRow(Transform parent, string label, float y, float width, bool initial, System.Action<bool> onChange, string tip = null)
    {
        TMP_Text text = PixelUIKit.CreateText(font, parent, label + " Label", label, fontSize,
                                              TextAlignmentOptions.MidlineLeft, FontStyles.Normal, textColor);
        Place(text.rectTransform, 40f, y, width - rowHeight - 10f);

        Toggle toggle = PixelUIKit.CreateTickBox(parent, rowHeight, tickBoxColor, tickColor);
        GameObject boxGo = toggle.gameObject;
        RectTransform br = boxGo.GetComponent<RectTransform>();
        br.anchorMin = br.anchorMax = br.pivot = new Vector2(0f, 1f);
        br.anchoredPosition = new Vector2(40f + width - rowHeight, -y);

        toggle.isOn = initial;
        toggle.onValueChanged.AddListener(on => onChange(on));
        toggle.onValueChanged.AddListener(_ => PixelAudio.Play("ui_click"));
        if (!string.IsNullOrEmpty(tip)) { AddTip(text.gameObject, tip); AddTip(boxGo, tip); }
        return toggle;
    }

    // ------------------------------------------------------------------
    // Icon buttons + hover tooltips
    // ------------------------------------------------------------------

    /// <summary>A square button with a picture on it (the picture is drawn in code).</summary>
    private Button IconButton(Transform parent, string name, Sprite icon, Color color, float size)
    {
        Button b = PixelUIKit.CreateButton(font, parent, name, "", new Vector2(size, size), color, textColor, fontSize);
        GameObject go = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(b.transform, false);
        Image img = go.GetComponent<Image>();
        img.sprite = icon;
        img.preserveAspect = true;
        img.raycastTarget = false;
        RectTransform r = go.GetComponent<RectTransform>();
        PixelUIKit.Stretch(r);
        r.offsetMin = new Vector2(size * 0.14f, size * 0.14f);
        r.offsetMax = new Vector2(-size * 0.14f, -size * 0.14f);
        return b;
    }

    private RectTransform tipRect;
    private TMP_Text tipLabel;
    private RectTransform tipCanvasRect;

    private void BuildTipBox()
    {
        tipCanvasRect = canvasRoot.GetComponent<RectTransform>();
        GameObject go = new GameObject("Tooltip", typeof(RectTransform), typeof(Image), typeof(PixelDevTip));
        go.transform.SetParent(canvasRoot.transform, false);
        go.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.08f, 0.96f);
        go.GetComponent<Image>().raycastTarget = false;
        tipRect = go.GetComponent<RectTransform>();
        tipRect.anchorMin = tipRect.anchorMax = new Vector2(0.5f, 0.5f);
        tipRect.pivot = new Vector2(0f, 1f);
        tipLabel = PixelUIKit.CreateText(font, go.transform, "Text", "", fontSize * 0.8f, TextAlignmentOptions.TopLeft, FontStyles.Normal, textColor);
        tipLabel.raycastTarget = false;
#if UNITY_2023_1_OR_NEWER
        tipLabel.textWrappingMode = TextWrappingModes.Normal;   // long tips wrap inside the box (max width below) instead of running off the screen on one line
#else
        tipLabel.enableWordWrapping = true;
#endif
        RectTransform lr = tipLabel.rectTransform;
        lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
        lr.offsetMin = new Vector2(12f, 8f); lr.offsetMax = new Vector2(-12f, -8f);
        go.GetComponent<PixelDevTip>().canvasRect = tipCanvasRect;
        go.GetComponent<PixelDevTip>().rect = tipRect;
        go.SetActive(false);
    }

    /// <summary>The Dev Tools' hover tooltip for any dev tool (the Look Studio...): shows 'text' next to the mouse while it is over 'target'. Does nothing before Dev Tools exists.</summary>
    public static void Tip(GameObject target, string text) { if (instance != null) instance.AddTip(target, text); }

    /// <summary>Shows 'text' in a small box next to the mouse while it is over 'target'.</summary>
    private void AddTip(GameObject target, string text)
    {
        if (target == null || tipRect == null || string.IsNullOrEmpty(text)) return;
        PixelHoverTip hover = target.GetComponent<PixelHoverTip>();
        if (hover == null) hover = target.AddComponent<PixelHoverTip>();
        hover.onEnter = () =>
        {
            tipLabel.text = text;
            Vector2 pref = tipLabel.GetPreferredValues(text, 560f, 0f);
            tipRect.sizeDelta = new Vector2(Mathf.Min(560f, pref.x) + 24f, pref.y + 16f);
            tipRect.gameObject.SetActive(true);
            tipRect.SetAsLastSibling();
        };
        hover.onExit = () => { if (tipRect != null) tipRect.gameObject.SetActive(false); };
    }
}

/// <summary>Keeps the dev tools' tooltip box next to the mouse while it is shown.</summary>
public class PixelDevTip : MonoBehaviour
{
    public RectTransform canvasRect, rect;

    private void LateUpdate()
    {
        if (canvasRect == null || rect == null) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, PixelInput.PointerPosition(), null, out Vector2 local)) return;
        PixelUIKit.PlaceTooltip(rect, canvasRect, local, 16f);   // bottom left of the pointer (bottom right / above when there is no room)
    }
}

/// <summary>Pictures for the dev tools, drawn in code: a plus, a paw print and a file, plus the window's rounded glowing frame.</summary>
public static class DevIcons
{
    private static Sprite plus, paw, file;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { plus = paw = file = null; }

    private const int N = 64;

    private static float Cover(float d) { return Mathf.Clamp01(0.5f - d); }   // anti-aliased edge from a signed distance in pixels

    private static float Ellipse(float x, float y, float cx, float cy, float rx, float ry)
    {
        float dx = (x - cx) / rx, dy = (y - cy) / ry;
        return (Mathf.Sqrt(dx * dx + dy * dy) - 1f) * Mathf.Min(rx, ry);
    }

    private static float Box(float x, float y, float cx, float cy, float hx, float hy)
    {
        float dx = Mathf.Abs(x - cx) - hx, dy = Mathf.Abs(y - cy) - hy;
        return Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f) + Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f)) + Mathf.Min(Mathf.Max(dx, dy), 0f);
    }

    private static Sprite Make(System.Func<float, float, Color> shade, string name)
    {
        Texture2D tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { name = name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        Color[] px = new Color[N * N];
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) px[y * N + x] = shade(x, y);
        tex.SetPixels(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
    }

    /// <summary>A bold black plus.</summary>
    public static Sprite PlusSprite()
    {
        if (plus == null)
            plus = Make((x, y) =>
            {
                float d = Mathf.Min(Box(x, y, 31.5f, 31.5f, 24f, 6.5f), Box(x, y, 31.5f, 31.5f, 6.5f, 24f)) - 2f;
                return new Color(0.04f, 0.04f, 0.04f, Cover(d));
            }, "Plus");
        return plus;
    }

    /// <summary>A dark paw print: a big pad and four toes.</summary>
    public static Sprite PawSprite()
    {
        if (paw == null)
            paw = Make((x, y) =>
            {
                float d = Ellipse(x, y, 32f, 21f, 17f, 13.5f);
                d = Mathf.Min(d, Ellipse(x, y, 11f, 38f, 6.5f, 8.5f));
                d = Mathf.Min(d, Ellipse(x, y, 24f, 50f, 6.5f, 8.5f));
                d = Mathf.Min(d, Ellipse(x, y, 40f, 50f, 6.5f, 8.5f));
                d = Mathf.Min(d, Ellipse(x, y, 53f, 38f, 6.5f, 8.5f));
                return new Color(0.2f, 0.12f, 0.04f, Cover(d));
            }, "Paw");
        return paw;
    }

    /// <summary>A sheet of paper with a folded corner and a few lines of text.</summary>
    public static Sprite FileSprite()
    {
        if (file == null)
            file = Make((x, y) =>
            {
                // The page: x 13..51, y 4..60, top right corner folded (cut off along a diagonal).
                float page = Box(x, y, 32f, 32f, 19f, 28f);
                float cut = (x + y) - (51f + 60f - 14f);   // > 0 beyond the diagonal at the top right
                float d = Mathf.Max(page, cut * 0.7071f);
                if (d > 0.5f)
                {
                    // The fold: a small triangle under the cut.
                    float fold = Mathf.Max(Mathf.Max(Box(x, y, 44f, 53f, 7f, 7f), -cut * 0.7071f), 0f);
                    return new Color(0.75f, 0.78f, 0.85f, Cover(fold - 0f) * (cut > 0f ? 1f : 0f));
                }
                float edge = Mathf.Clamp01(-d / 2.2f);                       // dark outline
                Color body = Color.Lerp(new Color(0.12f, 0.14f, 0.2f), new Color(0.96f, 0.97f, 1f), edge);
                bool line = (Mathf.Abs(y - 20f) < 1.8f || Mathf.Abs(y - 29f) < 1.8f || Mathf.Abs(y - 38f) < 1.8f) && x > 20f && x < 44f;
                if (line) body = new Color(0.45f, 0.5f, 0.62f);
                body.a = Cover(d);
                return body;
            }, "File");
        return file;
    }
}
