using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Toggles window: a button that slides out of the middle of the right edge of the screen opens a window with
/// every on/off switch in the game, in three groups (buttons at the top of the window):
///   Pixels    - switch the spawning of the special pixels (Vacuum, Obsidian, Singularity, Ghost, Meteor) on or off
///   Upgrades  - switch the Auto Clicker, Pixel Grabbing and Time Stop on or off
///   Minigames - switch each bought minigame on or off
/// Only things you own are listed. The choices are saved with the game.
/// It is a standard Fixed window (PixelUIKit.CreateWindow), anchored at the middle of the right edge (or beside the open shop).
///
/// Add this to any GameObject. The pause menu adds one automatically if the scene has none.
/// </summary>
public class PixelToggles : MonoBehaviour
{
    private const string MyWindowButtonName = "My Window Button";   // (the button object's name in the hierarchy, not shown to the player)

    private enum Group { Pixels = 0, Upgrades = 1, Minigames = 2, Vendors = 3, Pets = 4 }

    [Header("Search")]
    [Tooltip("Hint text in the Toggles window's search bar.")]
    [SerializeField] private string searchPlaceholder = "Search toggles...";
    private TMP_InputField searchField;
    private string searchText = "";
    private const int GroupCount = 5;

    [Header("References")]
    [Tooltip("The PixelClicker whose pixels are listed. Found automatically if left empty.")]
    [SerializeField] private PixelClicker clicker;

    [Tooltip("Fallback font, used only when the PixelClicker's 'UI Font' is empty.")]
    [SerializeField] private TMP_FontAsset font;

    [Header("Button")]
    [Tooltip("Text on the button at the edge of the screen.")]
    [SerializeField] private string buttonText = "Toggles";

    [Tooltip("Colour of the Toggles button and the My Window button at the screen edge (the Toggles' own purple, kept as a plain colour rather than a PixelUITheme role).")]
    [SerializeField] private Color buttonColor = new Color(0.45f, 0.35f, 0.75f, 1f);

    [Tooltip("Button text colour.")]
    [SerializeField] private Color buttonTextColor = Color.white;

    [Header("Window")]
    [Tooltip("Window title.")]
    [SerializeField] private string windowTitle = "Toggleable";

    [Tooltip("Names of the group buttons: Pixels, Upgrades, Minigames, Vendors, Pets (a missing name uses the default). Renamed from 'Group Names' so scenes saved with the four old names pick up the new tab.")]
    [SerializeField] private string[] tabNames = { "Pixels", "Upgrades", "Minigames", "Vendors", "Pets" };

    [Tooltip("Shown on the Vendors tab until a vendor has started visiting.")]
    [SerializeField] private string noVendorsText = "No vendors yet. They start visiting as you progress.";

    [Tooltip("Label of the 'Call' button on a buying vendor's row (calls him now; one call per PixelGuideVendor's call cooldown).")]
    [SerializeField] private string callButtonText = "Call";

    [Tooltip("The Call button while the call cooldown runs. {0} = minutes of play left.")]
    [SerializeField] private string callWaitFormat = "{0}m";

    [Tooltip("The Call button after you called him, until he arrives.")]
    [SerializeField] private string callComingText = "Coming";

    [Min(40f)]
    [Tooltip("Width of the Call button.")]
    [SerializeField] private float callButtonWidth = 110f;

    [Tooltip("Shown on the Pets tab while you have no pets.")]
    [SerializeField] private string noPetsText = "No pets yet. Very rarely, clicking a pixel gives you its pet.";

    [Tooltip("Shown when a group has nothing to toggle yet.")]
    [SerializeField] private string emptyText = "Nothing to toggle here yet.";

    [Tooltip("Label of the Auto Clicker toggle.")]
    [SerializeField] private string autoClickerLabel = "Auto Clicker";

    [Tooltip("Label of the Pixel Grabbing toggle.")]
    [SerializeField] private string grabbingLabel = "Pixel Grabbing";

    [Tooltip("Label of the UFO Helper switch (Upgrades tab).")]
    [SerializeField] private string ufoLabel = "UFO Helper";

    [Tooltip("Label of the Time Stop toggle.")]
    [SerializeField] private string timeStopLabel = "Time Stop";

    [Tooltip("Toggles window label of the Combo Meter.")]
    [SerializeField] private string comboLabel = "Combo Meter";

    [Tooltip("Keep the Toggles tab hidden until the Auto Clicker has been bought.")]
    [SerializeField] private bool hideUntilAutoClicker = true;

    [Tooltip("Window size (canvas units).")]
    [SerializeField] private Vector2 windowSize = new Vector2(640f, 720f);

    [Tooltip("Gap between the button and the window.")]
    [SerializeField] private float gapToButton = 14f;

    [Tooltip("Height of one toggle row.")]
    [SerializeField] private float rowHeight = 64f;

    [Tooltip("Text size.")]
    [SerializeField] private float fontSize = 30f;

    [Tooltip("Row background colour.")]
    [SerializeField] private Color rowColor = new Color(0.17f, 0.17f, 0.21f, 1f);

    [Tooltip("Background of the group drop-down and the search box. The group tab buttons use the Selected / Neutral roles in PixelUITheme.")]
    [SerializeField] private Color groupColor = new Color(0.22f, 0.22f, 0.28f, 1f);

    [Tooltip("Tick box colour.")]
    [SerializeField] private Color tickBoxColor = new Color(0.25f, 0.25f, 0.3f, 1f);

    [Tooltip("Tick colour.")]
    [SerializeField] private Color tickColor = new Color(0.4f, 0.9f, 0.5f, 1f);

    private Color textColor => PixelUITheme.TextColor;   // (the theme: PixelUITheme)

    [Tooltip("Scroll bar colour.")]
    [SerializeField] private Color scrollbarColor = new Color(1f, 1f, 1f, 0.35f);

    [Header("Hint Box (shown after buying the Auto Clicker)")]
    [TextArea(2, 4)]
    [Tooltip("Message in the box that points the player to this window.")]
    [SerializeField] private string hintText = "Auto Clicker bought!\nIt starts switched off. Open the Toggles window (the tab on the right edge of the screen), go to Upgrades and tick Auto Clicker.";

    [Tooltip("Text on the button that closes the hint box.")]
    [SerializeField] private string hintOkText = "Got it";

    [Min(0f)]
    [Tooltip("The hint box closes by itself after this many seconds. 0 = it stays until you close it or open the Toggles window.")]
    [SerializeField] private float hintSeconds = 0f;

    [Tooltip("Size of the hint box (canvas units).")]
    [SerializeField] private Vector2 hintSize = new Vector2(620f, 300f);

    [Tooltip("Hint box background colour.")]
    [SerializeField] private Color hintColor = new Color(0.12f, 0.2f, 0.3f, 0.98f);

    [Tooltip("Label of the row at the top of every tab that switches everything in that tab on or off at once.")]
    [SerializeField] private string toggleAllText = "Toggle all";

    [Header("Canvas")]
    [Tooltip("Sorting order of the canvas (above the inventory, below the log and shop).")]
    [SerializeField] private int sortingOrder = 130;

    [Tooltip("Reference resolution for the canvas scaler.")]
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);

    private class RowUI
    {
        public GameObject go;
        public TMP_Text label;
        public Toggle toggle;
        public Action<bool> setter;
        public Button edit;        // pet rows: the pencil that opens the pet's name window
        public Action editAction;
        public GameObject info;    // the orange '!' (rows with a note)
        public string note;
        public Button call;        // vendor rows: call him now (PixelVisitorMinigame.Call, shared cooldown)
        public TMP_Text callLabel;
        public Image callImage;
        public PixelVisitorMinigame callVendor;
    }

    private struct Entry
    {
        public string label;
        public bool on;
        public Action<bool> setter;
        public Action editAction;   // pet rows: opens the rename window (a pencil shows at the left of the name)
        public bool skipAll;   // not switched by the 'Toggle all' row (a mode switch rather than an on / off item)
        public string note;    // an orange '!' with this as its tooltip (e.g. 'only visits while training Woodcutting')
        public PixelVisitorMinigame callVendor;   // a vendor who buys from you: the row gets a 'Call' button
    }

    private GameObject canvasRoot, windowObject, emptyObject, hintObject, openObject;
    private TMP_Text emptyLabel;
    private float hintTimer;
    private static PixelToggles instance;
    private TMP_Dropdown groupDropdown;
    private GameObject[] groupTabs;
    private Image[] groupImages;
    private int litGroup = -1;   // the group whose tab was last shown Selected
    private bool besideShop; // true while the window is docked next to the open shop: drop-down instead of tabs
    private ScrollRect scroll;
    private RectTransform content;
    private GameObject bar;
    private readonly List<RowUI> rows = new List<RowUI>();
    private Group current = Group.Pixels;
    private float refreshTimer;
    private bool built;

    private PixelAutoClicker autoClicker;
    private PixelGrab grab;
    private PixelTimeStop timeStop;

    private void Awake()
    {
        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
        if (clicker == null)
        {
            Debug.LogError("PixelToggles: no PixelClicker found in the scene.", this);
            enabled = false;
            return;
        }
        if (clicker.UIFont != null) font = clicker.UIFont;
        instance = this;
    }

    /// <summary>The Auto Clicker hint text (for the event log).</summary>
    public static string HintMessage => instance != null ? instance.hintText : null;

    /// <summary>
    /// Shows the hint box next to the Toggles tab (which slides out to point at it). With no text, the box's own message is used.
    /// Closing it, or opening the Toggles window, dismisses it.
    /// </summary>
    public static void ShowHint(string message = null)
    {
        if (instance != null) instance.OpenHint(message);
    }

    private void OpenHint(string message)
    {
        if (!built || hintObject == null || windowObject.activeSelf) return;
        if (!string.IsNullOrEmpty(message)) hintObject.GetComponentInChildren<TMP_Text>().text = message;
        hintObject.SetActive(true);
        hintTimer = hintSeconds;
    }

    private void CloseHint()
    {
        if (hintObject != null) hintObject.SetActive(false);
    }

    private void Start()
    {
        PixelUIKit.EnsureEventSystem();
        BuildUI();
        built = true;
        if (PixelFind.First<PixelTogglesTutorial>() == null) new GameObject("Toggles Tutorial").AddComponent<PixelTogglesTutorial>();   // Cubie's intro to this window
    }

    /// <summary>The Toggles window's rectangle (null before it is built).</summary>
    public static RectTransform WindowRect => instance != null && instance.windowObject != null ? instance.windowObject.GetComponent<RectTransform>() : null;

    /// <summary>The Toggles tab at the screen edge.</summary>
    public static RectTransform TabRect => instance != null && instance.openObject != null ? instance.openObject.GetComponent<RectTransform>() : null;

    /// <summary>Is the Toggles tab shown (the Auto Clicker, a pet, a skill pack or a switchable pixel is owned)?</summary>
    public static bool TabVisible => instance != null && instance.openObject != null && instance.openObject.activeSelf;

    /// <summary>Tutorial: opens the window on a group (0 Pixels .. 4 Pets).</summary>
    public static void TutorialOpen(int group)
    {
        if (instance == null || !instance.built || instance.windowObject == null) return;
        instance.CloseHint();
        instance.current = (Group)Mathf.Clamp(group, 0, GroupCount - 1);
        if (instance.content != null) instance.content.anchoredPosition = Vector2.zero;
        if (!PixelPop.IsOpen(instance.windowObject)) PixelPop.Show(instance.windowObject);
        instance.refreshTimer = 0f;
        instance.Refresh();
    }

    /// <summary>Tutorial: closes the window.</summary>
    public static void TutorialClose() { CloseWindowIfOpen(); }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        PixelWindows.Unregister(this);
        if (canvasRoot != null) Destroy(canvasRoot);
    }

    private bool AnySwitchablePixel()
    {
        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
        if (clicker == null) return false;
        foreach (PixelClicker.PixelTier t in clicker.Tiers)
            if (t.unlocked && t.CanSwitchOff) return true;
        return false;
    }

    private void Update()
    {
        if (built && openObject != null)
        {
            if (autoClicker == null) autoClicker = PixelFind.Cached<PixelAutoClicker>();
            bool show = !hideUntilAutoClicker || (autoClicker != null && (autoClicker.Running || autoClicker.UserDisabled))
                        || (PixelPets.Instance != null && PixelPets.Instance.OwnedCount > 0) // a pet also opens the window
                        || (PixelSkills.Instance != null && PixelSkills.Instance.AnyPackAvailable) // so do the skill pack switches
                        || AnySwitchablePixel(); // and any pixel with a spawn switch (Glass...)
            if (openObject.activeSelf != show) openObject.SetActive(show);
            if (!show && windowObject != null && windowObject.activeSelf) windowObject.SetActive(false);
        }

        if (built && hintObject != null && hintObject.activeSelf && hintSeconds > 0f)
        {
            hintTimer -= Time.unscaledDeltaTime;
            if (hintTimer <= 0f) CloseHint();
        }

        if (!built || !windowObject.activeSelf) return;
        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer <= 0f)
        {
            refreshTimer = 0.25f;
            Refresh();
        }
    }

    // ------------------------------------------------------------------
    // The switches
    // ------------------------------------------------------------------

    private List<Entry> Collect(Group group)
    {
        List<Entry> list = new List<Entry>();

        if (group == Group.Pixels)
        {
            // (Skill packs are NOT listed here: the Skills window chooses the pack in play. Do not add skill-pack switches to the Toggles window.)
            PixelSkills skills = PixelSkills.Instance;
            if (skills != null)
            {
                // Skill-only blocks unlocked in a skill tree can be switched on for every rotation.
                foreach (PixelClicker.PixelType guest in skills.UnlockedGuests())
                {
                    PixelClicker.PixelType type = guest;
                    PixelSkills.SkillPack owner = skills.PackOfType(type);
                    list.Add(new Entry
                    {
                        label = ShortPixelName(type.ToString()) + " (" + (owner != null ? owner.displayName : "skill") + ")",
                        on = skills.GuestOn(type),
                        setter = on => skills.SetGuest(type, on),
                    });
                }
            }
            for (int i = 0; i < clicker.Tiers.Length; i++)
            {
                PixelClicker.PixelTier t = clicker.Tiers[i];
                if (!t.CanSwitchOff || !t.unlocked) continue;
                int index = i;
                list.Add(new Entry
                {
                    label = ShortPixelName(t.displayName),
                    on = !t.spawnDisabled,
                    setter = on => clicker.SetSpawnEnabled(index, on),
                });
            }
        }
        else if (group == Group.Upgrades)
        {
            if (autoClicker == null) autoClicker = PixelFind.First<PixelAutoClicker>();
            if (grab == null) grab = PixelFind.First<PixelGrab>();
            if (timeStop == null) timeStop = PixelFind.First<PixelTimeStop>();

            PixelAutoClicker ac = autoClicker;
            if (ac != null && (ac.Running || ac.UserDisabled))
                list.Add(new Entry { label = autoClickerLabel, on = !ac.UserDisabled, setter = on => ac.UserDisabled = !on });

            PixelGrab g = grab;
            if (g != null && (g.Active || g.UserDisabled))
                list.Add(new Entry { label = grabbingLabel, on = !g.UserDisabled, setter = on => g.UserDisabled = !on });

            PixelCombo combo = PixelFind.First<PixelCombo>();
            if (combo != null && (combo.Active || combo.UserDisabled))
                list.Add(new Entry { label = comboLabel, on = !combo.UserDisabled, setter = on => combo.UserDisabled = !on });

            PixelTimeStop ts = timeStop;
            if (ts != null && (ts.Active || ts.UserDisabled))
                list.Add(new Entry { label = timeStopLabel, on = !ts.UserDisabled, setter = on => ts.UserDisabled = !on });

            PixelRobotWorker ufo = PixelFind.First<PixelRobotWorker>();
            if (ufo != null && (ufo.Bought || ufo.UserDisabled))
                list.Add(new Entry { label = ufoLabel, on = !ufo.UserDisabled, setter = on => ufo.UserDisabled = !on });
        }
        else if (group == Group.Pets)
        {
            PixelPets pets = PixelPets.Instance;
            if (pets != null)
                foreach (KeyValuePair<PixelClicker.PixelType, string> pet in pets.Owned())
                {
                    PixelClicker.PixelType type = pet.Key;
                    list.Add(new Entry { label = pet.Value, on = pets.IsOn(type), setter = on => pets.SetOn(type, on), editAction = () => pets.EditPet(type) });
                }
        }
        else
        {
            // Minigames and Vendors (the visiting shopkeepers: Wizard, Tinkerer, Farmer, Entrepreneur) share the same on / off rows.
            bool vendors = group == Group.Vendors;
            foreach (PixelMinigame m in PixelMinigame.All)
            {
                if (m == null || !(m.Running || m.UserDisabled)) continue;
                if ((m is PixelVisitorMinigame) != vendors) continue;
                if (!m.CanSwitchOff)
                {
                    if (m.UserDisabled) { m.UserDisabled = false; m.Activate(); }   // (switched off before it became unswitchable: back on)
                    continue;   // no switch for it
                }
                PixelMinigame mg = m;
                list.Add(new Entry
                {
                    label = mg.DisplayName,
                    note = mg.ToggleNote,
                    callVendor = mg is PixelVisitorMinigame v && v.BuysFromPlayer ? v : null,
                    on = !mg.UserDisabled,
                    setter = on =>
                    {
                        mg.UserDisabled = !on;
                        if (on) mg.Activate(); else mg.Deactivate();
                    },
                });
            }
        }
        return list;
    }

    /// <summary>Picking a group tab leaves the search (the search covers every group, the tab picks one).</summary>
    private void ClearSearch()
    {
        searchText = "";
        if (searchField != null && searchField.text.Length > 0) searchField.SetTextWithoutNotify("");
    }

    private void Refresh()
    {
        if (groupDropdown != null && groupDropdown.value != (int)current) groupDropdown.SetValueWithoutNotify((int)current);
        if (groupImages != null && litGroup != (int)current)
        {
            litGroup = (int)current;   // (only when the group changed: SetRole searches every button)
            for (int i = 0; i < groupImages.Length; i++)
                PixelUIKit.SetRole(groupImages[i], i == litGroup ? ButtonRole.Selected : ButtonRole.Neutral);
        }
        ApplyGroupControl();

        bool searching = searchText.Length > 0;
        List<Entry> entries;
        if (searching)
        {
            // A search looks through EVERY group (named in front of each match) and replaces the list while it has text.
            entries = new List<Entry>();
            for (int g = 0; g < GroupCount; g++)
            {
                string groupName = tabNames != null && g < tabNames.Length && !string.IsNullOrEmpty(tabNames[g]) ? tabNames[g] : ((Group)g).ToString();
                foreach (Entry e in Collect((Group)g))
                    if ((e.label ?? "").ToLowerInvariant().Contains(searchText) || groupName.ToLowerInvariant().Contains(searchText))
                    {
                        Entry match = e;   // (a copy: the label gets the group name in front)
                        match.label = groupName + ": " + e.label;
                        entries.Add(match);
                    }
            }
        }
        else entries = Collect(current);
        entries.Sort((a, b) => string.Compare(a.label, b.label, System.StringComparison.OrdinalIgnoreCase));   // alphabetical
        List<Entry> switchable = entries.FindAll(e => !e.skipAll);
        if (!searching && switchable.Count >= 2)
        {
            // A 'Toggle all' row on top: ticked while everything below is on; clicking it sets everything to its new value.
            bool allOn = switchable.TrueForAll(e => e.on);
            entries.Insert(0, new Entry
            {
                label = toggleAllText,
                on = allOn,
                skipAll = true,
                setter = on =>
                {
                    foreach (Entry e in switchable) e.setter?.Invoke(on);
                    Refresh();
                },
            });
        }
        while (rows.Count < entries.Count) rows.Add(BuildRow());

        for (int i = 0; i < rows.Count; i++)
        {
            RowUI row = rows[i];
            bool used = i < entries.Count;
            if (row.go.activeSelf != used) row.go.SetActive(used);
            if (!used) continue;

            row.label.text = entries[i].label;
            row.setter = entries[i].setter;
            row.editAction = entries[i].editAction;
            row.edit.gameObject.SetActive(row.editAction != null);
            row.label.rectTransform.offsetMin = new Vector2(row.editAction != null ? rowHeight : 16f, 0f);
            row.toggle.SetIsOnWithoutNotify(entries[i].on);
            row.note = entries[i].note;
            bool hasNote = !string.IsNullOrEmpty(row.note);
            row.info.SetActive(hasNote);
            float rightUsed = (rowHeight + 20f) + (hasNote ? rowHeight * 0.62f + 10f : 0f);
            row.callVendor = entries[i].callVendor;
            bool hasCall = row.callVendor != null;
            if (row.call.gameObject.activeSelf != hasCall) row.call.gameObject.SetActive(hasCall);
            if (hasCall)
            {
                ((RectTransform)row.call.transform).anchoredPosition = new Vector2(-(rightUsed - 6f), 0f);
                rightUsed += callButtonWidth + 10f;
                RefreshCallButton(row);
            }
            row.label.rectTransform.offsetMax = new Vector2(-rightUsed, 0f);
            row.go.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -i * (rowHeight + 8f));
        }

        emptyObject.SetActive(entries.Count == 0);
        if (entries.Count == 0 && emptyLabel != null) emptyLabel.text = (searching ? "Nothing found" : current == Group.Pets ? noPetsText : current == Group.Vendors ? noVendorsText : emptyText).ToUpperInvariant();
        float contentHeight = entries.Count * (rowHeight + 8f);
        float viewHeight = scroll.GetComponent<RectTransform>().rect.height;
        PixelUIKit.UpdateScrollView(scroll, bar, Mathf.Max(contentHeight - 8f, 0f), viewHeight);
        UpdateTabTip();
    }

    /// <summary>The Call button's state: 'Call' when he can come, 'Coming' after a call, the minutes left while the shared cooldown runs.</summary>
    private void RefreshCallButton(RowUI row)
    {
        PixelVisitorMinigame v = row.callVendor;
        double wait = PixelVisitorMinigame.CallReadyIn;
        bool can = v.CanCall;
        string text = v.OnHisWay ? callComingText : wait > 0d ? string.Format(callWaitFormat, Mathf.CeilToInt((float)(wait / 60d))) : callButtonText;
        PixelUIKit.SetText(row.callLabel, text);
        row.call.interactable = can;
        PixelUIKit.SetRole(row.callImage, can ? ButtonRole.Success : ButtonRole.Neutral);
    }

    /// <summary>"Seed Pixels" -> "Seed": the Pixels group already says what the switches are for, and short names stay on one line at full size.</summary>
    private static string ShortPixelName(string name)
    {
        if (name.EndsWith(" Pixels")) return name.Substring(0, name.Length - 7);
        if (name.EndsWith(" Pixel")) return name.Substring(0, name.Length - 6);
        return name;
    }

    private RowUI BuildRow()
    {
        RowUI row = new RowUI();
        row.go = new GameObject("Toggle Row", typeof(RectTransform), typeof(Image));
        row.go.transform.SetParent(content, false);
        row.go.GetComponent<Image>().color = rowColor;
        PixelUIKit.StyleBox(row.go.GetComponent<Image>());
        RectTransform rr = row.go.GetComponent<RectTransform>();
        rr.anchorMin = new Vector2(0f, 1f);
        rr.anchorMax = new Vector2(1f, 1f);
        rr.pivot = new Vector2(0.5f, 1f);
        rr.sizeDelta = new Vector2(0f, rowHeight);

        row.label = PixelUIKit.CreateText(font, row.go.transform, "Label", "", fontSize, TextAlignmentOptions.MidlineLeft,
                                          FontStyles.Normal, textColor);
        OneLine(row.label, fontSize, 14f);   // one line: a long name shrinks instead of wrapping into two
        RectTransform lr = row.label.rectTransform;
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = Vector2.one;
        lr.offsetMin = new Vector2(16f, 0f);
        lr.offsetMax = new Vector2(-(rowHeight + 20f), 0f);

        // The pencil (pet rows only) on the far left.
        GameObject pencil = new GameObject("Pencil", typeof(RectTransform), typeof(Image));
        pencil.transform.SetParent(row.go.transform, false);
        Image pencilImage = pencil.GetComponent<Image>();
        PixelPets petsForIcon = PixelPets.Instance;
        pencilImage.sprite = petsForIcon != null ? petsForIcon.PencilSprite() : null;
        pencilImage.preserveAspect = true;
        RectTransform pencilRect = pencil.GetComponent<RectTransform>();
        pencilRect.anchorMin = pencilRect.anchorMax = pencilRect.pivot = new Vector2(0f, 0.5f);
        pencilRect.sizeDelta = new Vector2(rowHeight * 0.62f, rowHeight * 0.62f);
        pencilRect.anchoredPosition = new Vector2(12f, 0f);
        row.edit = PixelUIKit.MakeClickable(pencil, () => row.editAction?.Invoke());
        pencil.SetActive(false);

        GameObject box = new GameObject("Tick Box", typeof(RectTransform), typeof(Image), typeof(Toggle));
        box.transform.SetParent(row.go.transform, false);
        Image bg = box.GetComponent<Image>();
        bg.color = tickBoxColor;
        PixelUIKit.StyleBox(bg, true);   // a sunken box
        float size = rowHeight * 0.72f;
        RectTransform br = box.GetComponent<RectTransform>();
        br.anchorMin = br.anchorMax = br.pivot = new Vector2(1f, 0.5f);
        br.sizeDelta = new Vector2(size, size);
        br.anchoredPosition = new Vector2(-14f, 0f);

        GameObject tick = new GameObject("Tick", typeof(RectTransform), typeof(Image));
        tick.transform.SetParent(box.transform, false);
        Image ti = tick.GetComponent<Image>();
        ti.sprite = PixelUIKit.TickSprite();
        ti.color = tickColor;
        ti.raycastTarget = false;
        RectTransform kr = tick.GetComponent<RectTransform>();
        PixelUIKit.Stretch(kr);
        kr.offsetMin = new Vector2(size * 0.1f, size * 0.1f);
        kr.offsetMax = new Vector2(-size * 0.1f, -size * 0.1f);

        // The orange '!' left of the tick box (rows with a note): hover it for the note.
        float infoSize = rowHeight * 0.62f;
        RectTransform ir = PixelUIKit.CreateInfoBadge(font, row.go.transform, infoSize, () => hoveredNote = row.note, () => { if (hoveredNote == row.note) hoveredNote = null; });
        ir.anchorMin = ir.anchorMax = ir.pivot = new Vector2(1f, 0.5f);
        ir.anchoredPosition = new Vector2(-14f - size - 10f, 0f);
        row.info = ir.gameObject;
        row.info.SetActive(false);

        // The 'Call' button (rows of vendors who buy from you), placed left of the '!' / tick box in Refresh.
        row.call = PixelUIKit.CreateButton(font, row.go.transform, "Call", callButtonText, new Vector2(callButtonWidth, size), ButtonRole.Neutral, fontSize * 0.8f);
        RectTransform cr = (RectTransform)row.call.transform;
        cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(1f, 0.5f);
        cr.sizeDelta = new Vector2(callButtonWidth, size);
        row.callImage = row.call.GetComponent<Image>();
        row.callLabel = row.call.GetComponentInChildren<TMP_Text>(true);
        row.call.onClick.AddListener(() => { if (row.callVendor != null && row.callVendor.Call()) Refresh(); });
        row.call.gameObject.SetActive(false);

        row.toggle = box.GetComponent<Toggle>();
        row.toggle.targetGraphic = bg;
        row.toggle.graphic = ti;
        row.toggle.onValueChanged.AddListener(on =>
        {
            row.setter?.Invoke(on);
            PixelAudio.Play("ui_click");
        });
        return row;
    }

    // ------------------------------------------------------------------
    // Building the UI
    // ------------------------------------------------------------------

    private void BuildUI()
    {
        canvasRoot = PixelUIKit.CreateCanvas("PixelToggles Canvas", sortingOrder, referenceResolution, true);

        // --- The button, docked to the middle of the right edge.
        Button open = PixelUIKit.CreateButton(font, canvasRoot.transform, "Toggles Button", buttonText, new Vector2(230f, 64f),
                                              buttonColor, buttonTextColor, 30f);
        PixelHud hud = PixelHud.Ensure(gameObject);
        // The tab is narrower, with a square My Window button on its inner side (the Bank tab and its hose button work the same way on the left edge).
        float squareSize = hud.ButtonSize.y, tabWidth = hud.ButtonSize.x - squareSize;
        myWindowButton = PixelUIKit.CreateButton(font, canvasRoot.transform, MyWindowButtonName, "", new Vector2(squareSize, squareSize),
                                                 buttonColor, buttonTextColor, 20f);
        System.Func<bool> keepOut = () => (windowObject != null && windowObject.activeSelf) || (hintObject != null && hintObject.activeSelf)   // stays out while its hint is showing
                                          || (dock != null && dock.PointerNear()) || (myDock != null && myDock.PointerNear());
        // Bottom-right corner of the bars: the Toggles tab in the corner, the square My Window button beside it (and the Pixel Guide button next to those).
        hud.Dock(open.GetComponent<RectTransform>(), new Vector2(1f, 0f), keepOut, 0f, tabWidth / hud.ButtonSize.x, 0f);
        hud.Dock(myWindowButton.GetComponent<RectTransform>(), new Vector2(1f, 0f), keepOut, 0f, squareSize / hud.ButtonSize.x, tabWidth + hud.CornerGap * 0.5f);
        myDock = myWindowButton.GetComponent<PixelDockedButton>();
        myGroup = myWindowButton.gameObject.AddComponent<CanvasGroup>();
        BuildMyWindowIcon(myWindowButton, squareSize);
        myWindowButton.onClick.AddListener(PixelMyWindow.ToggleOpen);
        open.onClick.AddListener(Toggle);
        PixelMenuKeys.Register(PixelAction.Toggles, open);
        openObject = open.gameObject;
        dock = openObject.GetComponent<PixelDockedButton>();
        buttonGroup = openObject.AddComponent<CanvasGroup>();

        // --- The window, just left of the button.
        // The standard Fixed window (PixelUIKit.CreateWindow: title, grey X, PixelPop). The builder centres it; this one is put back at the
        // middle of the right edge, taking the spot the button slides out to (FitBesideShop moves it next to the shop while that is open).
        PixelWindowParts parts = PixelUIKit.CreateWindow(font, canvasRoot.transform, "Toggles Window", windowTitle, windowSize,
                                                         PixelUIKit.WindowKind.Fixed, Close);
        windowObject = parts.Root;
        RectTransform wr = parts.Rect;
        wr.anchorMin = wr.anchorMax = wr.pivot = new Vector2(1f, 0.5f);
        wr.anchoredPosition = new Vector2(-(hud.SideShownInset), 0f);

        // The contents line up with the Shop's (also a standard window): the first element under the header starts at the shop's tab row, is as tall, and the list starts where the shop's list does.
        PixelShop layoutShop = PixelFind.First<PixelShop>();
        float headerH = parts.HeaderHeight;
        float tabH = layoutShop != null ? layoutShop.TabHeightValue : 70f;
        float listTop = layoutShop != null ? layoutShop.ListTopValue : headerH + tabH + 10f;
        float sidePad = layoutShop != null ? layoutShop.PanelPaddingValue : 20f;
        PixelMenuFx.AddWindowLine(windowObject.transform, headerH - 8f);   // between the title and the icon row

        float y = headerH;   // same y as the shop's tab row

        // --- The group drop-down (Pixels / Upgrades / Minigames / Vendors / Pets).
        List<string> groupLabels = new List<string>();
        for (int i = 0; i < GroupCount; i++)
            groupLabels.Add(tabNames != null && i < tabNames.Length && !string.IsNullOrEmpty(tabNames[i]) ? tabNames[i] : ((Group)i).ToString());
        groupDropdown = PixelUIKit.CreateDropdown(font, windowObject.transform, "Group Dropdown", new Vector2(0f, tabH), groupColor,
                                                  new Color(groupColor.r * 0.8f, groupColor.g * 0.8f, groupColor.b * 0.8f, 1f), textColor, fontSize);
        groupDropdown.ClearOptions();
        groupDropdown.AddOptions(groupLabels);
        groupDropdown.SetValueWithoutNotify((int)current);
        RectTransform gr = groupDropdown.GetComponent<RectTransform>();
        gr.anchorMin = new Vector2(0f, 1f);
        gr.anchorMax = new Vector2(1f, 1f);
        gr.pivot = new Vector2(0.5f, 1f);
        gr.offsetMin = new Vector2(sidePad, -(y + tabH));
        gr.offsetMax = new Vector2(-sidePad, -y);
        groupDropdown.onValueChanged.AddListener(value =>
        {
            current = (Group)value;
            ClearSearch();
            content.anchoredPosition = Vector2.zero;
            Refresh();
        });

        BuildGroupTabs(groupLabels, y, tabH, sidePad);   // the same choice as tab buttons
        // --- The search bar under the tabs: looks through every group.
        float searchH = 52f;
        searchField = PixelUIKit.CreateInputField(font, windowObject.transform, "Toggle Search", new Vector2(0f, searchH), groupColor, textColor, fontSize * 0.8f, searchPlaceholder);
        PixelUIKit.AddSearchIcon(searchField);
        RectTransform sbr = searchField.GetComponent<RectTransform>();
        sbr.anchorMin = new Vector2(0f, 1f); sbr.anchorMax = new Vector2(1f, 1f); sbr.pivot = new Vector2(0.5f, 1f);
        sbr.offsetMin = new Vector2(sidePad, -(listTop - 2f + searchH)); sbr.offsetMax = new Vector2(-sidePad, -(listTop - 2f));
        searchField.onValueChanged.AddListener(text =>
        {
            searchText = (text ?? "").Trim().ToLowerInvariant();
            content.anchoredPosition = Vector2.zero;
            Refresh();
        });
        y = listTop + searchH + 6f;   // the list starts under the search bar

        // --- The list of switches.
        scroll = PixelUIKit.CreateScrollView(windowObject.transform, "Toggle List", scrollbarColor, 12f, rowHeight, out content, out bar);
        PixelScrollBarFade barFade = bar != null ? bar.GetComponent<PixelScrollBarFade>() : null;
        if (barFade != null) barFade.hideBar = true;   // no visible scroll bar here (the list still scrolls with the wheel, and fades at the bottom)
        RectTransform vr = scroll.GetComponent<RectTransform>();
        vr.anchorMin = new Vector2(0f, 0f);
        vr.anchorMax = new Vector2(1f, 1f);
        vr.offsetMin = new Vector2(sidePad, sidePad);
        vr.offsetMax = new Vector2(-sidePad, -y);

        TMP_Text empty = PixelUIKit.CreateText(font, windowObject.transform, "Empty", emptyText.ToUpperInvariant(), fontSize * 0.9f,
                                               TextAlignmentOptions.Center, FontStyles.Bold,
                                               new Color(textColor.r, textColor.g, textColor.b, 0.6f));
        OneLine(empty, fontSize * 0.9f, 8f);
        RectTransform er = empty.rectTransform; // capitals, centred over the whole list area
        er.anchorMin = Vector2.zero;
        er.anchorMax = Vector2.one;
        er.offsetMin = new Vector2(sidePad, sidePad);
        er.offsetMax = new Vector2(-sidePad, -y);
        emptyObject = empty.gameObject;
        emptyLabel = empty;

        BuildHint(hud);   // the hint box, where the window would be

        windowObject.SetActive(false);
        PixelWindows.Register(this, 15, () => windowObject != null && PixelPop.IsOpen(windowObject), Close);
    }

    // BuildUI, step: the group tab buttons (one per group, a pixel-art icon each) under the header.
    private void BuildGroupTabs(List<string> groupLabels, float y, float tabH, float sidePad)
    {
        // The same choice as tab buttons: used while the window stands alone (the drop-down is for beside the shop).
        groupImages = new Image[GroupCount];
        litGroup = -1;
        groupTabs = new GameObject[GroupCount];
        for (int i = 0; i < GroupCount; i++)
        {
            Button b = PixelUIKit.CreateButton(font, windowObject.transform, "Group " + groupLabels[i], "", Vector2.zero, ButtonRole.Neutral, fontSize);
            groupTabs[i] = b.gameObject;
            groupImages[i] = b.GetComponent<Image>();

            // A pixel-art icon instead of a name; the name shows in a tip when the mouse is over the tab.
            GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(RawImage));
            iconGo.transform.SetParent(b.transform, false);
            RawImage icon = iconGo.GetComponent<RawImage>();
            icon.texture = PixelTabIcons.Get(i);
            icon.raycastTarget = false;
            RectTransform ir = iconGo.GetComponent<RectTransform>();
            ir.anchorMin = ir.anchorMax = ir.pivot = new Vector2(0.5f, 0.5f);
            float iconSize = Mathf.Max(24f, tabH - 14f);
            ir.sizeDelta = new Vector2(iconSize, iconSize);
            int tipIndex = i;
            string tipName = groupLabels[i];
            PixelHoverTip hoverTip = b.gameObject.AddComponent<PixelHoverTip>();
            hoverTip.onEnter = () => { hoveredTab = tipIndex; hoveredTabName = tipName; };
            hoverTip.onExit = () => { if (hoveredTab == tipIndex) hoveredTab = -1; };
            RectTransform br = b.GetComponent<RectTransform>();
            br.anchorMin = new Vector2(i / (float)GroupCount, 1f);
            br.anchorMax = new Vector2((i + 1) / (float)GroupCount, 1f);
            br.pivot = new Vector2(0.5f, 1f);
            br.offsetMin = new Vector2(i == 0 ? sidePad : 4f, -(y + tabH));
            br.offsetMax = new Vector2(i == GroupCount - 1 ? -sidePad : -4f, -y);

            int captured = i;
            b.onClick.AddListener(() =>
            {
                current = (Group)captured;
                ClearSearch();
                content.anchoredPosition = Vector2.zero;
                Refresh();
            });
        }
    }

    // BuildUI, step: the hint box - sits where the window would, next to the slid-out tab.
    private void BuildHint(PixelHud hud)
    {
        // --- The hint box: sits where the window would, next to the slid-out tab.
        hintObject = new GameObject("Toggles Hint", typeof(RectTransform), typeof(Image));
        hintObject.transform.SetParent(canvasRoot.transform, false);
        hintObject.GetComponent<Image>().color = hintColor;
        RectTransform hr = hintObject.GetComponent<RectTransform>();
        hr.anchorMin = hr.anchorMax = hr.pivot = new Vector2(1f, 0.5f);
        hr.sizeDelta = hintSize;
        hr.anchoredPosition = new Vector2(-(hud.ButtonSize.x + gapToButton + hud.SideShownInset), 0f);

        TMP_Text hintLabel = PixelUIKit.CreateText(font, hintObject.transform, "Hint Text", hintText, fontSize * 0.95f,
                                                   TextAlignmentOptions.Center, FontStyles.Normal, textColor);
        RectTransform tr2 = hintLabel.rectTransform;
        tr2.anchorMin = Vector2.zero;
        tr2.anchorMax = Vector2.one;
        tr2.offsetMin = new Vector2(24f, 90f);
        tr2.offsetMax = new Vector2(-24f, -20f);

        Button ok = PixelUIKit.CreateButton(font, hintObject.transform, "Hint Ok", hintOkText, new Vector2(200f, 60f), ButtonRole.Success, fontSize);
        RectTransform okr = ok.GetComponent<RectTransform>();
        okr.anchorMin = okr.anchorMax = okr.pivot = new Vector2(0.5f, 0f);
        okr.anchoredPosition = new Vector2(0f, 20f);
        ok.onClick.AddListener(CloseHint);
        hintObject.SetActive(false);
    }

    private void Toggle()
    {
        if (PixelPop.IsOpen(windowObject)) Close();
        else
        {
            CloseHint(); // the player found the window
            PixelPixelGuide.CloseWindowIfOpen();
            PixelPop.Show(windowObject);
            refreshTimer = 0f;
            Refresh();
        }
    }

    private void Close() => PixelPop.Hide(windowObject);

    /// <summary>Closes the window if it is open (the Pixel Guide takes the same spot on the right).</summary>
    public static void CloseWindowIfOpen()
    {
        if (instance != null && instance.windowObject != null && PixelPop.IsOpen(instance.windowObject)) instance.Close();
    }

    /// <summary>Keeps a text on one line, shrinking it to fit the width (the shared PixelUIKit.FitOneLine).</summary>
    private static void OneLine(TMP_Text t, float maxSize, float minSize)
    {
        t.fontSize = maxSize;
        PixelUIKit.FitOneLine(t, minSize);
    }

    private PixelShop shopRef;

    /// <summary>With the shop open the window is as tall as it and as wide as the Currency panel, flush against the shop (the shared PixelUIKit.FitBesideShop).</summary>
    private void FitBesideShop()
    {
        if (shopRef == null) shopRef = PixelFind.First<PixelShop>();
        SetBesideShop(PixelUIKit.FitBesideShop(windowObject.GetComponent<RectTransform>(), shopRef, windowSize));
    }

    private void SetBesideShop(bool value)
    {
        if (besideShop == value) return;
        besideShop = value;
        ApplyGroupControl();
    }

    /// <summary>Drop-down while docked beside the shop (narrow), tab buttons when the window stands alone.</summary>
    private void ApplyGroupControl()
    {
        // The icon tabs are used both beside the shop and standing alone (the drop-down is no longer shown).
        if (groupDropdown != null && groupDropdown.gameObject.activeSelf) groupDropdown.gameObject.SetActive(false);
        if (groupTabs != null)
            foreach (GameObject tab in groupTabs)
                if (tab != null && !tab.activeSelf) tab.SetActive(true);
        UpdateTabTip();
    }

    private int hoveredTab = -1;
    private string hoveredTabName = "";
    private RectTransform tabTipRect;
    private TMP_Text tabTipLabel;

    /// <summary>The little name box under the tab the mouse is over.</summary>
    private void UpdateTabTip()
    {
        if (windowObject == null || groupTabs == null) return;
        if (tabTipRect == null)
        {
            GameObject go = new GameObject("Tab Tip", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(windowObject.transform, false);
            Image bg = go.GetComponent<Image>();
            bg.color = new Color(0.04f, 0.05f, 0.09f, 0.96f);
            bg.raycastTarget = false;
            PixelUIKit.StyleBox(bg);
            tabTipRect = go.GetComponent<RectTransform>();
            tabTipRect.anchorMin = tabTipRect.anchorMax = new Vector2(0f, 1f);
            tabTipRect.pivot = new Vector2(0.5f, 1f);
            tabTipLabel = PixelUIKit.CreateText(font, go.transform, "Text", "", fontSize, TextAlignmentOptions.Center, FontStyles.Bold, new Color(1f, 0.88f, 0.4f, 1f));
            PixelUIKit.Caps(tabTipLabel);
            tabTipLabel.raycastTarget = false;
            PixelUIKit.Stretch(tabTipLabel.rectTransform);
            go.SetActive(false);
        }
        bool show = hoveredTab >= 0 && hoveredTab < groupTabs.Length && groupTabs[hoveredTab] != null && groupTabs[hoveredTab].activeInHierarchy;
        if (tabTipRect.gameObject.activeSelf != show) tabTipRect.gameObject.SetActive(show);
        if (!show) return;
        tabTipRect.SetAsLastSibling();
        PixelUIKit.SetText(tabTipLabel, hoveredTabName);
        RectTransform tabRect = groupTabs[hoveredTab].GetComponent<RectTransform>();
        Vector3[] c = new Vector3[4];
        tabRect.GetWorldCorners(c);
        Vector3 bottomCentre = new Vector3((c[0].x + c[3].x) * 0.5f, c[0].y, c[0].z);
        float w = Mathf.Max(120f, hoveredTabName.Length * fontSize * 0.75f + 36f);
        tabTipRect.sizeDelta = new Vector2(w, fontSize * 1.9f);
        tabTipRect.position = bottomCentre + Vector3.down * (6f * tabTipRect.lossyScale.y);
    }

    private PixelDockedButton dock;
    private Button myWindowButton;
    private PixelDockedButton myDock;
    private CanvasGroup myGroup;
    private CanvasGroup buttonGroup;
    /// <summary>The window picture on the square My Window button, plus a small name box that shows while the mouse is over it.</summary>
    private void BuildMyWindowIcon(Button button, float size)
    {
        float k = size / 64f;
        GameObject winIcon = new GameObject("Window Icon", typeof(RectTransform));
        winIcon.transform.SetParent(button.transform, false);
        RectTransform winIr = winIcon.GetComponent<RectTransform>();
        winIr.anchorMin = winIr.anchorMax = winIr.pivot = new Vector2(0.5f, 0.5f);
        winIr.sizeDelta = new Vector2(38f, 32f) * k;
        void Part(Vector2 sz, Vector2 at, Color c)
        {
            GameObject g = new GameObject("Part", typeof(RectTransform), typeof(Image));
            g.transform.SetParent(winIcon.transform, false);
            g.GetComponent<Image>().color = c;
            g.GetComponent<Image>().raycastTarget = false;
            RectTransform r = g.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = sz * k;
            r.anchoredPosition = at * k;
        }
        Part(new Vector2(38f, 32f), Vector2.zero, new Color(0.85f, 0.9f, 1f));                 // the window frame
        Part(new Vector2(32f, 20f), new Vector2(0f, -4f), new Color(0.12f, 0.14f, 0.2f));     // its inside
        Part(new Vector2(38f, 7f), new Vector2(0f, 12.5f), new Color(0.35f, 0.6f, 1f));        // the title bar
        Part(new Vector2(22f, 3f), new Vector2(-2f, 0f), new Color(0.85f, 0.9f, 1f));          // a couple of lines
        Part(new Vector2(16f, 3f), new Vector2(-5f, -7f), new Color(0.85f, 0.9f, 1f));
        GameObject tip = new GameObject("My Window Tip", typeof(RectTransform), typeof(Image));
        tip.transform.SetParent(button.transform, false);
        Image tipBg = tip.GetComponent<Image>();
        tipBg.color = new Color(0.04f, 0.05f, 0.09f, 0.96f);
        tipBg.raycastTarget = false;
        PixelUIKit.StyleBox(tipBg);
        RectTransform tipRect = tip.GetComponent<RectTransform>();
        tipRect.anchorMin = tipRect.anchorMax = new Vector2(0.5f, 0f);
        tipRect.pivot = new Vector2(1f, 1f);
        tipRect.sizeDelta = new Vector2(230f, fontSize * 1.9f);
        tipRect.anchoredPosition = new Vector2(size * 0.5f, -6f);
        TMP_Text tipLabel = PixelUIKit.CreateText(font, tip.transform, "Text", "Pocket", fontSize, TextAlignmentOptions.Center, FontStyles.Bold, new Color(1f, 0.88f, 0.4f, 1f));
        PixelUIKit.Caps(tipLabel);
        PixelUIKit.Stretch(tipLabel.rectTransform);
        tip.SetActive(false);
        PixelHoverTip hover = button.gameObject.AddComponent<PixelHoverTip>();
        hover.onEnter = () => { tip.transform.SetAsLastSibling(); tip.SetActive(true); };
        hover.onExit = () => tip.SetActive(false);
    }

    private string hoveredNote;
    private RectTransform noteTipRect;
    private TMP_Text noteTipLabel;

    /// <summary>The tooltip of a row's orange '!' (beside the pointer while it is over one).</summary>
    private void UpdateNoteTip()
    {
        if (canvasRoot == null) return;
        bool show = !string.IsNullOrEmpty(hoveredNote) && windowObject != null && windowObject.activeInHierarchy;
        if (noteTipRect == null)
        {
            if (!show) return;
            GameObject go = new GameObject("Note Tip", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(canvasRoot.transform, false);
            Image bg = go.GetComponent<Image>();
            bg.color = new Color(0.04f, 0.05f, 0.09f, 0.96f);
            bg.raycastTarget = false;
            PixelUIKit.StyleBox(bg);
            noteTipRect = go.GetComponent<RectTransform>();
            noteTipRect.anchorMin = noteTipRect.anchorMax = new Vector2(0.5f, 0.5f);
            noteTipLabel = PixelUIKit.CreateText(font, go.transform, "Text", "", fontSize * 0.85f, TextAlignmentOptions.MidlineLeft, FontStyles.Normal, Color.white);
            noteTipLabel.raycastTarget = false;
            noteTipLabel.textWrappingMode = TextWrappingModes.Normal;
            PixelUIKit.Stretch(noteTipLabel.rectTransform);
            noteTipLabel.margin = new Vector4(14f, 8f, 14f, 8f);
        }
        if (noteTipRect.gameObject.activeSelf != show) noteTipRect.gameObject.SetActive(show);
        if (!show) return;
        noteTipRect.SetAsLastSibling();
        noteTipLabel.text = hoveredNote;
        Vector2 pref = noteTipLabel.GetPreferredValues(hoveredNote, 360f - 28f, 0f);
        noteTipRect.sizeDelta = new Vector2(Mathf.Min(360f, pref.x + 30f), pref.y + 20f);
        RectTransform canvasRect = (RectTransform)canvasRoot.transform;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, PixelInput.PointerPosition(), null, out Vector2 local))
            PixelUIKit.PlaceTooltip(noteTipRect, canvasRect, local);
    }

    private void LateUpdate()
    {
        UpdateTabTip();
        UpdateNoteTip();
        if (buttonGroup == null || windowObject == null) return;
        bool open = windowObject.activeSelf;
        if (open) FitBesideShop();
        if (myWindowButton != null && myWindowButton.gameObject.activeSelf != PixelMyWindow.Available) myWindowButton.gameObject.SetActive(PixelMyWindow.Available);   // My Window shows up once two pixel types are unlocked
        // The buttons stay where they are while the window is open, so they can be clicked again to close it.
        if (dock != null) dock.ForceHidden = false;
        if (myDock != null) myDock.ForceHidden = false;
        buttonGroup.alpha = 1f;
        buttonGroup.blocksRaycasts = true;
        if (myGroup != null) { myGroup.blocksRaycasts = true; myGroup.alpha = 1f; }
    }
}
