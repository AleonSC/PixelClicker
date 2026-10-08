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

    [Tooltip("Label of the add-pixels button.")]
    [SerializeField] private string addText = "Add";

    [Tooltip("First entry of the pixel drop-down: adds the amount to every pixel type.")]
    [SerializeField] private string allPixelsText = "All pixels";

    [Tooltip("Label of the disable-despawns tick box.")]
    [SerializeField] private string noDespawnToggleText = "Disable old pixel despawns";

    [Tooltip("Label of the god-pixel-mode tick box.")]
    [SerializeField] private string godToggleText = "God pixel mode (scroll = pick, left = spawn, right = destroy)";

    [Tooltip("Label of the minigame spawn button.")]
    [SerializeField] private string spawnText = "Spawn";

    [Tooltip("Label of the skip-intro button.")]
    [SerializeField] private string skipIntroText = "Skip intro (Gray, Black, RGB, Auto Clicker)";

    [Tooltip("Label of the unlock-everything button.")]
    [SerializeField] private string unlockAllText = "Unlock all shop items";

    [Tooltip("Label of the clear-old-pixels tick box.")]
    [SerializeField] private string clearToggleText = "Press Q to clear old pixels";

    [Tooltip("Label of the infinite-resources tick box.")]
    [SerializeField] private string infiniteToggleText = "Infinite resources";

    [Tooltip("Label of the spawn-selector tick box.")]
    [SerializeField] private string spawnSelectorToggleText = "On-screen spawn selector";

    [Tooltip("First entry of the on-screen selector (normal random spawning).")]
    [SerializeField] private string spawnNormalText = "Spawn: normal (random)";

    [Tooltip("Entry that spawns only White, Gray and Black.")]
    [SerializeField] private string spawnMonochromeText = "Spawn: Monochrome pack";

    [Tooltip("Entry that spawns only Red, Green and Blue.")]
    [SerializeField] private string spawnRgbText = "Spawn: RGB pack";

    [Tooltip("Size of the on-screen pixel selector (canvas units).")]
    [SerializeField] private Vector2 selectorSize = new Vector2(440f, 56f);

    [Tooltip("Width of the on-screen minigame drop-down next to it (canvas units).")]
    [SerializeField] private float minigameSelectorWidth = 340f;

    [Tooltip("Width of the on-screen minigame Spawn button (canvas units).")]
    [SerializeField] private float spawnButtonWidth = 130f;

    [Tooltip("Label of the close button.")]
    [SerializeField] private string closeText = "Close";

    [Header("Look")]
    [Tooltip("Panel size (canvas units). It grows if the rows need more room.")]
    [SerializeField] private Vector2 panelSize = new Vector2(900f, 700f);

    [Tooltip("Height of each row.")]
    [SerializeField] private float rowHeight = 64f;

    [Tooltip("Text size of rows and buttons.")]
    [SerializeField] private float fontSize = 30f;

    [Tooltip("Title text size.")]
    [SerializeField] private float titleFontSize = 48f;

    [Tooltip("Dark background behind the panel.")]
    [SerializeField] private Color dimColor = new Color(0f, 0f, 0f, 0.6f);

    [Tooltip("Panel background colour.")]
    [SerializeField] private Color panelColor = new Color(0.12f, 0.12f, 0.16f, 0.98f);

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
    [Tooltip("Sorting order of the canvas. Must be above the pause menu (500) so the panel is not hidden by it.")]
    [SerializeField] private int panelSortingOrder = 700;

    [Tooltip("Reference resolution for the canvas scaler.")]
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);

    private static PixelDevTools instance;
    private GameObject canvasRoot;
    private GameObject panel;
    private TMP_Dropdown pixelDropdown;
    private TMP_InputField amountField;
    private readonly List<PixelMinigame> dropdownMinigames = new List<PixelMinigame>();

    /// <summary>True when dev tools exist (Editor / development build). The pause menu uses this to show its button.</summary>
    public static bool Available => instance != null;

    /// <summary>The text for the pause menu's button.</summary>
    public static string ButtonText => instance != null ? instance.title : "Dev Tools";

    /// <summary>Is the dev tools panel showing?</summary>
    public static bool IsOpen => instance != null && instance.panel != null && instance.panel.activeSelf;

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
        PixelClicker.GodMode = godMode;
    }

    // ------------------------------------------------------------------
    // Remembered settings (PlayerPrefs)
    // ------------------------------------------------------------------

    private const string PrefPrefix = "PixelClicker.Dev.";

    private bool GetBool(string key, bool fallback) =>
        !rememberSettings ? fallback : PlayerPrefs.GetInt(PrefPrefix + key, fallback ? 1 : 0) != 0;

    private void SetBool(string key, bool value)
    {
        if (!rememberSettings) return;
        PlayerPrefs.SetInt(PrefPrefix + key, value ? 1 : 0);
        PlayerPrefs.Save();
    }

    private int GetInt(string key, int fallback) => !rememberSettings ? fallback : PlayerPrefs.GetInt(PrefPrefix + key, fallback);

    private void SetInt(string key, int value)
    {
        if (!rememberSettings) return;
        PlayerPrefs.SetInt(PrefPrefix + key, value);
        PlayerPrefs.Save();
    }

    private void LoadSettings()
    {
        clearKeyEnabled = GetBool("ClearKey", clearKeyEnabled);
        infiniteResources = GetBool("Infinite", infiniteResources);
        spawnSelectorEnabled = GetBool("Selector", spawnSelectorEnabled);
        disableDespawn = GetBool("NoDespawn", disableDespawn);
        godMode = GetBool("God", godMode);
        if (rememberSettings) defaultAmount = PlayerPrefs.GetString(PrefPrefix + "Amount", defaultAmount);
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
        if (instance == this)
        {
            instance = null;
            PixelClicker.InfiniteResources = false;
        }
        if (canvasRoot != null) Destroy(canvasRoot);
        if (selectorRoot != null) Destroy(selectorRoot);
        if (godRoot != null) Destroy(godRoot);
        PixelClicker.GodMode = false;
        OldPixelDespawn.DevNoDespawn = false;
        if (spawnSelectorEnabled && clicker != null && clicker.isActiveAndEnabled) clicker.SetDevSpawnTiers(null);
    }

    private void Update()
    {
        UpdateGod();
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
        if (alsoAddUltra && amount >= 1d) clicker.AddUltra(tierIndex, (long)System.Math.Floor(amount));
    }

    private void AddSelected()
    {
        if (pixelDropdown == null || amountField == null) return;
        string text = amountField.text.Trim().Replace(",", "");
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double amount)) return;
        if (pixelDropdown.value == 0) AddToAll(amount); // "All pixels"
        else GiveAmount(pixelDropdown.value - 1, amount);
    }


    private void SkipIntro()
    {
        if (shop != null) shop.DevSkipIntro();
    }

    private void UnlockAll()
    {
        if (shop != null) shop.DevUnlockAll();
    }

    // ------------------------------------------------------------------
    // UI
    // ------------------------------------------------------------------

    private void Open()
    {
        if (panel == null) return;
        FillDropdowns();
        panel.SetActive(true);
    }

    private void Close()
    {
        if (panel != null) panel.SetActive(false);
    }

    private void FillDropdowns()
    {
        PixelClicker.PixelTier[] tiers = clicker.Tiers;
        int keep = pixelDropdown.value;
        List<string> names = new List<string>();
        names.Add(allPixelsText);
        foreach (PixelClicker.PixelTier t in tiers) names.Add(t.unlocked ? t.displayName : t.displayName + " (locked)");
        pixelDropdown.ClearOptions();
        pixelDropdown.AddOptions(names);
        pixelDropdown.SetValueWithoutNotify(Mathf.Clamp(keep, 0, Mathf.Max(0, names.Count - 1)));
        pixelDropdown.RefreshShownValue();

    }

    private void BuildPanel()
    {
        canvasRoot = PixelUIKit.CreateCanvas("PixelDevTools Canvas", panelSortingOrder, referenceResolution, true);

        // Full-screen dimmer that also blocks clicks behind the panel.
        GameObject dim = new GameObject("Dev Tools", typeof(RectTransform), typeof(Image));
        dim.transform.SetParent(canvasRoot.transform, false);
        dim.GetComponent<Image>().color = dimColor;
        PixelUIKit.Stretch(dim.GetComponent<RectTransform>());
        panel = dim;

        GameObject box = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(dim.transform, false);
        box.GetComponent<Image>().color = panelColor;
        RectTransform boxRect = box.GetComponent<RectTransform>();
        boxRect.anchorMin = boxRect.anchorMax = boxRect.pivot = new Vector2(0.5f, 0.5f);
        boxRect.anchoredPosition = Vector2.zero;

        TMP_Text t = PixelUIKit.CreateText(font, box.transform, "Title", title, titleFontSize,
                                           TextAlignmentOptions.Center, FontStyles.Bold, textColor);
        RectTransform tr = t.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f); tr.pivot = new Vector2(0.5f, 1f);
        tr.sizeDelta = new Vector2(0f, titleFontSize * 1.6f);
        tr.anchoredPosition = new Vector2(0f, -24f);

        float y = 24f + titleFontSize * 1.6f + 16f;
        float inner = panelSize.x - 80f; // usable width (40 px each side)

        // Row: pixel dropdown | amount field | Add
        float dropW = inner * 0.42f, fieldW = inner * 0.30f, btnW = inner - dropW - fieldW - 20f;
        pixelDropdown = PixelUIKit.CreateDropdown(font, box.transform, "Pixel Dropdown", new Vector2(dropW, rowHeight),
                                                  boxColor, listColor, textColor, fontSize);
        Place(pixelDropdown.GetComponent<RectTransform>(), 40f, y, dropW);
        amountField = PixelUIKit.CreateInputField(font, box.transform, "Amount Field", new Vector2(fieldW, rowHeight),
                                                  boxColor, textColor, fontSize, "amount");
        amountField.text = defaultAmount;
        amountField.onValueChanged.AddListener(text => { if (rememberSettings) PlayerPrefs.SetString(PrefPrefix + "Amount", text); });
        Place(amountField.GetComponent<RectTransform>(), 40f + dropW + 10f, y, fieldW);
        Button add = PixelUIKit.CreateButton(font, box.transform, "Add Button", addText, new Vector2(btnW, rowHeight),
                                             buttonColor, textColor, fontSize);
        Place(add.GetComponent<RectTransform>(), 40f + dropW + fieldW + 20f, y, btnW);
        add.onClick.AddListener(AddSelected);
        y += rowHeight + 16f;

        // Row: skip intro
        Button skip = PixelUIKit.CreateButton(font, box.transform, "Skip Intro Button", skipIntroText,
                                              new Vector2(inner, rowHeight), buttonColor, textColor, fontSize);
        Place(skip.GetComponent<RectTransform>(), 40f, y, inner);
        skip.onClick.AddListener(SkipIntro);
        y += rowHeight + 24f;

        // Row: unlock all
        Button unlock = PixelUIKit.CreateButton(font, box.transform, "Unlock All Button", unlockAllText,
                                                new Vector2(inner, rowHeight), buttonColor, textColor, fontSize);
        Place(unlock.GetComponent<RectTransform>(), 40f, y, inner);
        unlock.onClick.AddListener(UnlockAll);
        y += rowHeight + 24f;

        // Row: tick box for the clear key
        BuildToggleRow(box.transform, clearToggleText, y, inner, clearKeyEnabled, on => { clearKeyEnabled = on; SetBool("ClearKey", on); });
        y += rowHeight + 24f;

        // Row: tick box for infinite resources
        BuildToggleRow(box.transform, infiniteToggleText, y, inner, infiniteResources,
                       on => { infiniteResources = on; PixelClicker.InfiniteResources = on; SetBool("Infinite", on); });
        y += rowHeight + 24f;

        // Row: tick box for the on-screen spawn selector
        BuildToggleRow(box.transform, spawnSelectorToggleText, y, inner, spawnSelectorEnabled, SetSelector);
        y += rowHeight + 24f;

        // Row: tick box to stop old pixels despawning
        BuildToggleRow(box.transform, noDespawnToggleText, y, inner, disableDespawn, on =>
        {
            disableDespawn = on;
            OldPixelDespawn.DevNoDespawn = on;
            SetBool("NoDespawn", on);
        });
        y += rowHeight + 24f;

        // Row: tick box for god pixel mode
        BuildToggleRow(box.transform, godToggleText, y, inner, godMode, SetGodMode);
        y += rowHeight + 24f;

        // Close
        Button close = PixelUIKit.CreateButton(font, box.transform, "Close Button", closeText,
                                               new Vector2(inner, rowHeight), new Color(0.35f, 0.35f, 0.42f, 1f), textColor, fontSize);
        Place(close.GetComponent<RectTransform>(), 40f, y, inner);
        close.onClick.AddListener(Close);
        y += rowHeight + 30f;

        boxRect.sizeDelta = new Vector2(panelSize.x, Mathf.Max(panelSize.y, y));

        FillDropdowns();
        pixelDropdown.SetValueWithoutNotify(Mathf.Clamp(GetInt("PixelIndex", 0), 0, pixelDropdown.options.Count - 1));
        pixelDropdown.RefreshShownValue();
        pixelDropdown.onValueChanged.AddListener(v => SetInt("PixelIndex", v));
        panel.SetActive(false);
        if (spawnSelectorEnabled) SetSelector(true);
        if (godMode) SetGodMode(true);
    }

    // ------------------------------------------------------------------
    // On-screen spawn selector
    // ------------------------------------------------------------------

    private GameObject selectorRoot;
    private TMP_Dropdown selectorDropdown;
    private readonly List<PixelClicker.PixelType[]> selectorChoices = new List<PixelClicker.PixelType[]>();

    private TMP_Dropdown selectorMinigameDropdown;

    private void SetSelector(bool on)
    {
        spawnSelectorEnabled = on;
        SetBool("Selector", on);
        if (clicker == null) return;
        if (!on)
        {
            if (selectorRoot != null) selectorRoot.SetActive(false);
            clicker.SetDevSpawnTiers(null);
            return;
        }

        if (selectorRoot == null) BuildSelector();
        FillSelector();
        selectorRoot.SetActive(true);
        ApplySelector(selectorDropdown.value);
    }

    /// <summary>The on-screen row at the top: pixel drop-down, then minigame drop-down + Spawn button.</summary>
    private void BuildSelector()
    {
        selectorRoot = PixelUIKit.CreateCanvas("PixelDevTools Spawn Selector", 145, referenceResolution, true);
        selectorRoot.transform.SetParent(transform, false);

        const float gap = 10f;
        float total = selectorSize.x + gap + minigameSelectorWidth + gap + spawnButtonWidth;
        float x = -total * 0.5f;
        float bar = PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f;
        float top = -(bar + 12f);

        selectorDropdown = PixelUIKit.CreateDropdown(font, selectorRoot.transform, "Spawn Dropdown", selectorSize,
                                                     boxColor, listColor, textColor, fontSize);
        PlaceTop(selectorDropdown.GetComponent<RectTransform>(), x, top, selectorSize);
        selectorDropdown.onValueChanged.AddListener(v => { SetInt("SelPixel", v); ApplySelector(v); });
        x += selectorSize.x + gap;

        Vector2 mgSize = new Vector2(minigameSelectorWidth, selectorSize.y);
        selectorMinigameDropdown = PixelUIKit.CreateDropdown(font, selectorRoot.transform, "Minigame Dropdown", mgSize,
                                                             boxColor, listColor, textColor, fontSize);
        PlaceTop(selectorMinigameDropdown.GetComponent<RectTransform>(), x, top, mgSize);
        selectorMinigameDropdown.onValueChanged.AddListener(v => SetInt("SelMinigame", v));
        x += minigameSelectorWidth + gap;

        Vector2 btnSize = new Vector2(spawnButtonWidth, selectorSize.y);
        Button spawn = PixelUIKit.CreateButton(font, selectorRoot.transform, "Spawn Button", spawnText, btnSize,
                                               buttonColor, textColor, fontSize);
        PlaceTop(spawn.GetComponent<RectTransform>(), x, top, btnSize);
        spawn.onClick.AddListener(SpawnSelected);
    }

    private static void PlaceTop(RectTransform rt, float x, float y, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = size;
        rt.anchoredPosition = new Vector2(x, y);
    }

    /// <summary>Pixel options: normal, Monochrome pack, RGB pack, then every pixel type (locked ones marked). Plus the minigame list.</summary>
    private void FillSelector()
    {
        bool first = selectorChoices.Count == 0;
        int keep = first ? GetInt("SelPixel", 0) : selectorDropdown.value;
        selectorChoices.Clear();
        List<string> names = new List<string>();

        names.Add(spawnNormalText); selectorChoices.Add(null);
        names.Add(spawnMonochromeText);
        selectorChoices.Add(new[] { PixelClicker.PixelType.White, PixelClicker.PixelType.Gray, PixelClicker.PixelType.Black });
        names.Add(spawnRgbText);
        selectorChoices.Add(new[] { PixelClicker.PixelType.Red, PixelClicker.PixelType.Green, PixelClicker.PixelType.Blue });

        foreach (PixelClicker.PixelTier tier in clicker.Tiers)
        {
            names.Add(tier.displayName + (tier.unlocked ? "" : " (locked)"));
            selectorChoices.Add(new[] { tier.type });
        }

        selectorDropdown.ClearOptions();
        selectorDropdown.AddOptions(names);
        selectorDropdown.SetValueWithoutNotify(Mathf.Clamp(keep, 0, names.Count - 1));
        selectorDropdown.RefreshShownValue();

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
        selectorMinigameDropdown.ClearOptions();
        selectorMinigameDropdown.AddOptions(names);
        selectorMinigameDropdown.SetValueWithoutNotify(Mathf.Clamp(keepGame, 0, Mathf.Max(0, names.Count - 1)));
        selectorMinigameDropdown.RefreshShownValue();
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
        bool active = !PixelPauseMenu.IsPaused && !IsOpen && !PixelTitleScreen.Showing;
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

        Camera cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
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
                if (plane.Raycast(ray, out float enter))
                    clicker.SpawnStoredPixel(godTiers[godChoice], tier.amountPerClick, ray.GetPoint(enter), Random.insideUnitSphere * 0.6f);
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

    private void BuildToggleRow(Transform parent, string label, float y, float width, bool initial, System.Action<bool> onChange)
    {
        TMP_Text text = PixelUIKit.CreateText(font, parent, label + " Label", label, fontSize,
                                              TextAlignmentOptions.MidlineLeft, FontStyles.Normal, textColor);
        Place(text.rectTransform, 40f, y, width - rowHeight - 10f);

        GameObject boxGo = new GameObject("Tick Box", typeof(RectTransform), typeof(Image), typeof(Toggle));
        boxGo.transform.SetParent(parent, false);
        Image bg = boxGo.GetComponent<Image>();
        bg.color = tickBoxColor;
        RectTransform br = boxGo.GetComponent<RectTransform>();
        br.anchorMin = br.anchorMax = br.pivot = new Vector2(0f, 1f);
        br.sizeDelta = new Vector2(rowHeight, rowHeight);
        br.anchoredPosition = new Vector2(40f + width - rowHeight, -y);

        GameObject tick = new GameObject("Tick", typeof(RectTransform), typeof(Image));
        tick.transform.SetParent(boxGo.transform, false);
        Image tickImage = tick.GetComponent<Image>();
        tickImage.color = tickColor;
        tickImage.raycastTarget = false;
        RectTransform kr = tick.GetComponent<RectTransform>();
        PixelUIKit.Stretch(kr);
        kr.offsetMin = new Vector2(rowHeight * 0.2f, rowHeight * 0.2f);
        kr.offsetMax = new Vector2(-rowHeight * 0.2f, -rowHeight * 0.2f);

        Toggle toggle = boxGo.GetComponent<Toggle>();
        toggle.targetGraphic = bg;
        toggle.graphic = tickImage;
        toggle.isOn = initial;
        toggle.onValueChanged.AddListener(on => onChange(on));
        toggle.onValueChanged.AddListener(_ => PixelAudio.Play("ui_click"));
    }
}
