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
/// Part files (partial class): PixelDevToolsPanel.cs (the Main tab widget board), PixelDevToolsSpawn.cs (spawn selector + spawn / cheat
/// widget cards), PixelDevToolsGod.cs (god pixel mode), PixelDevToolsDocs.cs (the To-Do and Changelog views).
/// </summary>
public partial class PixelDevTools : MonoBehaviour
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

    [Tooltip("Start with 'Disable husk despawns' ticked: husks never expire on their own.")]
    [SerializeField] private bool disableDespawn = false;

    [Tooltip("Start with 'Disable husk limit' ticked: any number of husks can lie around (the husk floor cap is ignored).")]
    [SerializeField] private bool disableOldPixelLimit = false;

    [Tooltip("Start with 'God pixel mode' ticked: the mouse spawns pixels (left click) and destroys them (right click); scroll to choose the pixel.")]
    [SerializeField] private bool godMode = false;

    [Min(1f)]
    [Tooltip("God pixel mode: pixels spawned per second while the left button is held.")]
    [SerializeField] private float godSpawnRate = 14f;

    [Min(5f)]
    [Tooltip("God pixel mode: how close (screen pixels) a husk must be to the cursor to be destroyed by a right click.")]
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

    [Tooltip("Start with the 'Press Q to clear husks' box ticked.")]
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
    [SerializeField] private string noDespawnToggleText = "Disable husk despawns";

    [Tooltip("Label of the tick box that removes the limit on husks.")]
    [SerializeField] private string noLimitToggleText = "Disable husk limit";

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
    [SerializeField] private string godTipText = "Scroll = pick a pixel, left click = spawn it, right click = destroy husks near the cursor";

    [Tooltip("Label of the tick box that turns on Edit text mode (click any text in the game to change it; saved to Resources/TextEdits.txt).")]
    [SerializeField] private string textEditModeLabel = "Edit text mode";
    [Tooltip("Hover tip of the Edit text mode tick box.")]
    [SerializeField] private string textEditModeTip = "Click any text in the game to change it. Saved to Resources/TextEdits.txt and used everywhere that text shows.";

    [Tooltip("Label of the minigame spawn button.")]
    [SerializeField] private string spawnText = "Spawn";

    [Tooltip("Tooltip of the file button at the top left that opens the folder with the crash / error reports (for sending to the developer).")]
    [SerializeField] private string reportTipText = "Open the crash / error report folder";

    [Tooltip("Text of the button that sets every pixel count to 0 (inventory, Log and Bank) but keeps unlocks.")]
    [SerializeField] private string resetCountsText = "Reset all counts to 0";

    [Tooltip("Text the reset button shows after the first click (click again within a few seconds to confirm).")]
    [SerializeField] private string resetConfirmText = "Click again to erase all counts";

    [Tooltip("Label of the clear-husks tick box.")]
    [SerializeField] private string clearToggleText = "Press Q to clear husks";

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

    [Tooltip("'Cubie's bill' widget: the button that reveals the bill now (or opens the Bill window once revealed).")]
    [SerializeField] private string billRevealText = "Reveal";
    [Tooltip("'Cubie's bill' widget: the button that pays the next milestone for free (the next trust tier opens).")]
    [SerializeField] private string billNextTierText = "Tier +1";
    [Tooltip("'Cubie's bill' widget: the button that resets the bill (nothing paid, tier 0, not revealed).")]
    [SerializeField] private string billResetText = "Reset";

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

    [Tooltip("Label of the 'Clear world' widget's button: removes every placed device and every tester object (it asks once more first).")]
    [SerializeField] private string clearWorldText = "Clear all placed";
    [Tooltip("The same button's label while it waits for the second click that confirms.")]
    [SerializeField] private string clearWorldConfirmText = "Click again to clear";

    [Tooltip("Text of the 'Dev items' widget's button that puts the picked dev-only device in your inventory.")]
    [SerializeField] private string giveDevItemText = "Give";

    [Tooltip("Label of the click-count row (how many clicks one click counts as).")]
    [SerializeField] private string clickCountText = "Clicks per click";

    [Header("Look")]


    [Tooltip("Height of each row.")]
    [SerializeField] private float rowHeight = 64f;
    [Tooltip("Hover text of the Time control widget's buttons.")]
    [SerializeField] private string timeControlTipText = "Rewind / Pause / Play / Slow / Fast. Press Slow, Fast or Rewind again for the next speed; while one is on, the mouse wheel changes the speed by 0.1x (0.5x above 2x; 0.1x - 10x). Rewind only moves husks back - currency, timers and devices stay as they are.";

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
    [Tooltip("Colour of the tick in the Dev Tools' tick boxes.")]
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
    private Toggle clearToggle, infiniteToggle, noDespawnToggle, noLimitToggle, godToggle, textEditToggle;

    // The Edit text mode tick box follows the mode (its banner's Done button / Escape turn it off too).
    private void OnTextEditModeChanged(bool on) { if (textEditToggle != null) textEditToggle.SetIsOnWithoutNotify(on); }

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
        PixelTextEditMode.Changed -= OnTextEditModeChanged;
        PixelDevHub.OpenChanged -= OnHubOpenChanged;
        PixelDevDocs.Shown -= OnDocsShown;
        PixelTimeControl.Changed -= RefreshTimeStatus;
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
        if (!PixelGameMode.TitleShowing)
        {
            if (PixelKeys.Pressed(PixelAction.DevTools)) { if (IsOpen) Close(); else Open(); }
            if (PixelKeys.Pressed(PixelAction.MarkMoment)) PixelDevProbe.MarkMoment();
            if (PixelKeys.Pressed(PixelAction.InspectHover)) PixelDevProbe.Inspect();
        }
        UpdateGod();
        UpdateMiddleSwap();
        if (!clearKeyEnabled || clicker == null || PixelGameMode.PauseMenuOpen) return;
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
        PixelDebug.Info(what + " in " + boundsSnapshotDelay + " s - close this panel and set up what you want to show.");
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

/// <summary>Pictures for the dev tools, drawn in code: a plus and a paw print, plus the window's rounded glowing frame.</summary>
public static class DevIcons
{
    private static Sprite plus, paw;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { plus = paw = null; }

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
}
