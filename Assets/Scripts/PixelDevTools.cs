using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Developer tools for Pixel Clicker. Adds a "Dev Tools" button to the pause menu (only in the Editor and Development
/// Builds) that opens a panel with:
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
    [Tooltip("Amount the 'Add to all' button gives each pixel type.")]
    [SerializeField] private double amountToAdd = 10;

    [Tooltip("Does 'Add to all' also include tiers that aren't unlocked yet? Off = only unlocked tiers.")]
    [SerializeField] private bool includeLockedTiers = false;

    [Tooltip("The Add buttons also give the same amount of Ultra pixels of that type (rounded down to a whole number).")]
    [SerializeField] private bool alsoAddUltra = true;

    [Tooltip("Amount filled into the text field when the panel is first built.")]
    [SerializeField] private string defaultAmount = "100";

    [Tooltip("Start with the 'Press Q to clear old pixels' box ticked.")]
    [SerializeField] private bool clearKeyEnabled = false;

    [Tooltip("Start with 'Infinite resources' ticked: everything in the shop and crafting costs nothing.")]
    [SerializeField] private bool infiniteResources = false;

    [Tooltip("Only exist in the Editor and Development Builds. In a release build this component removes itself.")]
    [SerializeField] private bool devBuildsOnly = true;

    [Header("Text")]
    [Tooltip("Title of the dev tools panel (also the text of the pause menu button).")]
    [SerializeField] private string title = "Dev Tools";

    [Tooltip("Label of the add-pixels button.")]
    [SerializeField] private string addText = "Add";

    [Tooltip("Label of the add-to-all button. {0} = the amount.")]
    [SerializeField] private string addAllText = "Add +{0} to all pixels";

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
    private TMP_Dropdown minigameDropdown;
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
        if (devBuildsOnly && !Debug.isDebugBuild) { Destroy(this); return; }
        instance = this;
        PixelClicker.InfiniteResources = infiniteResources;
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
    }

    private void Update()
    {
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
    public void AddToAll()
    {
        PixelClicker.PixelTier[] tiers = clicker.Tiers;
        for (int i = 0; i < tiers.Length; i++)
        {
            if (!includeLockedTiers && !tiers[i].unlocked) continue;
            GiveAmount(i, amountToAdd);
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
        GiveAmount(pixelDropdown.value, amount);
    }

    private void SpawnSelected()
    {
        if (minigameDropdown == null || minigameDropdown.value < 0 || minigameDropdown.value >= dropdownMinigames.Count) return;
        PixelMinigame game = dropdownMinigames[minigameDropdown.value];
        if (game != null) game.SpawnNow();
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
        foreach (PixelClicker.PixelTier t in tiers) names.Add(t.unlocked ? t.displayName : t.displayName + " (locked)");
        pixelDropdown.ClearOptions();
        pixelDropdown.AddOptions(names);
        pixelDropdown.SetValueWithoutNotify(Mathf.Clamp(keep, 0, Mathf.Max(0, names.Count - 1)));
        pixelDropdown.RefreshShownValue();

        keep = minigameDropdown.value;
        dropdownMinigames.Clear();
        names.Clear();
        foreach (PixelMinigame game in PixelMinigame.All)
        {
            if (game == null) continue;
            dropdownMinigames.Add(game);
            names.Add(game.DisplayName);
        }
        minigameDropdown.ClearOptions();
        minigameDropdown.AddOptions(names);
        minigameDropdown.SetValueWithoutNotify(Mathf.Clamp(keep, 0, Mathf.Max(0, names.Count - 1)));
        minigameDropdown.RefreshShownValue();
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
        Place(amountField.GetComponent<RectTransform>(), 40f + dropW + 10f, y, fieldW);
        Button add = PixelUIKit.CreateButton(font, box.transform, "Add Button", addText, new Vector2(btnW, rowHeight),
                                             buttonColor, textColor, fontSize);
        Place(add.GetComponent<RectTransform>(), 40f + dropW + fieldW + 20f, y, btnW);
        add.onClick.AddListener(AddSelected);
        y += rowHeight + 16f;

        // Row: add to all
        Button all = PixelUIKit.CreateButton(font, box.transform, "Add All Button",
                                             string.Format(addAllText, PixelClicker.FormatNumber(amountToAdd)),
                                             new Vector2(inner, rowHeight), buttonColor, textColor, fontSize);
        Place(all.GetComponent<RectTransform>(), 40f, y, inner);
        all.onClick.AddListener(AddToAll);
        y += rowHeight + 24f;

        // Row: minigame dropdown | Spawn
        float spawnW = inner * 0.28f, mgW = inner - spawnW - 10f;
        minigameDropdown = PixelUIKit.CreateDropdown(font, box.transform, "Minigame Dropdown", new Vector2(mgW, rowHeight),
                                                     boxColor, listColor, textColor, fontSize);
        Place(minigameDropdown.GetComponent<RectTransform>(), 40f, y, mgW);
        Button spawn = PixelUIKit.CreateButton(font, box.transform, "Spawn Button", spawnText, new Vector2(spawnW, rowHeight),
                                               buttonColor, textColor, fontSize);
        Place(spawn.GetComponent<RectTransform>(), 40f + mgW + 10f, y, spawnW);
        spawn.onClick.AddListener(SpawnSelected);
        y += rowHeight + 24f;

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
        BuildToggleRow(box.transform, clearToggleText, y, inner, clearKeyEnabled, on => clearKeyEnabled = on);
        y += rowHeight + 24f;

        // Row: tick box for infinite resources
        BuildToggleRow(box.transform, infiniteToggleText, y, inner, infiniteResources,
                       on => { infiniteResources = on; PixelClicker.InfiniteResources = on; });
        y += rowHeight + 24f;

        // Close
        Button close = PixelUIKit.CreateButton(font, box.transform, "Close Button", closeText,
                                               new Vector2(inner, rowHeight), new Color(0.35f, 0.35f, 0.42f, 1f), textColor, fontSize);
        Place(close.GetComponent<RectTransform>(), 40f, y, inner);
        close.onClick.AddListener(Close);
        y += rowHeight + 30f;

        boxRect.sizeDelta = new Vector2(panelSize.x, Mathf.Max(panelSize.y, y));

        FillDropdowns();
        panel.SetActive(false);
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
