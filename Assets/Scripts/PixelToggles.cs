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
///
/// Add this to any GameObject. The pause menu adds one automatically if the scene has none.
/// </summary>
public class PixelToggles : MonoBehaviour
{
    private enum Group { Pixels = 0, Upgrades = 1, Minigames = 2 }

    [Header("References")]
    [Tooltip("The PixelClicker whose pixels are listed. Found automatically if left empty.")]
    [SerializeField] private PixelClicker clicker;

    [Tooltip("Fallback font, used only when the PixelClicker's 'UI Font' is empty.")]
    [SerializeField] private TMP_FontAsset font;

    [Header("Button")]
    [Tooltip("Text on the button at the edge of the screen.")]
    [SerializeField] private string buttonText = "Toggles";

    [Tooltip("Button colour.")]
    [SerializeField] private Color buttonColor = new Color(0.45f, 0.35f, 0.75f, 1f);

    [Tooltip("Button text colour.")]
    [SerializeField] private Color buttonTextColor = Color.white;

    [Header("Window")]
    [Tooltip("Window title.")]
    [SerializeField] private string windowTitle = "Toggleable";

    [Tooltip("Names of the three group buttons: Pixels, Upgrades, Minigames.")]
    [SerializeField] private string[] groupNames = { "Pixels", "Upgrades", "Minigames" };

    [Tooltip("Shown when a group has nothing to toggle yet.")]
    [SerializeField] private string emptyText = "Nothing to toggle here yet.";

    [Tooltip("Label of the Auto Clicker toggle.")]
    [SerializeField] private string autoClickerLabel = "Auto Clicker";

    [Tooltip("Label of the Pixel Grabbing toggle.")]
    [SerializeField] private string grabbingLabel = "Pixel Grabbing";

    [Tooltip("Label of the Time Stop toggle.")]
    [SerializeField] private string timeStopLabel = "Time Stop";

    [Tooltip("Toggles window label of the Combo Meter.")]
    [SerializeField] private string comboLabel = "Combo Meter";

    [Tooltip("Label of a pixel's toggle. {0} = pixel name.")]
    [SerializeField] private string pixelLabelFormat = "{0} spawn";

    [Tooltip("Window size (canvas units).")]
    [SerializeField] private Vector2 windowSize = new Vector2(640f, 720f);

    [Tooltip("Gap between the button and the window.")]
    [SerializeField] private float gapToButton = 14f;

    [Tooltip("Height of one toggle row.")]
    [SerializeField] private float rowHeight = 64f;

    [Tooltip("Text size.")]
    [SerializeField] private float fontSize = 30f;

    [Tooltip("Title text size.")]
    [SerializeField] private float titleFontSize = 44f;

    [Tooltip("Window background colour.")]
    [SerializeField] private Color panelColor = new Color(0.1f, 0.1f, 0.13f, 0.97f);

    [Tooltip("Row background colour.")]
    [SerializeField] private Color rowColor = new Color(0.17f, 0.17f, 0.21f, 1f);

    [Tooltip("Group button colour (not selected).")]
    [SerializeField] private Color groupColor = new Color(0.22f, 0.22f, 0.28f, 1f);

    [Tooltip("Group button colour (selected).")]
    [SerializeField] private Color groupActiveColor = new Color(0.45f, 0.35f, 0.75f, 1f);

    [Tooltip("Tick box colour.")]
    [SerializeField] private Color tickBoxColor = new Color(0.25f, 0.25f, 0.3f, 1f);

    [Tooltip("Tick colour.")]
    [SerializeField] private Color tickColor = new Color(0.4f, 0.9f, 0.5f, 1f);

    [Tooltip("Text colour.")]
    [SerializeField] private Color textColor = Color.white;

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
    }

    private struct Entry
    {
        public string label;
        public bool on;
        public Action<bool> setter;
    }

    private GameObject canvasRoot, windowObject, emptyObject, hintObject;
    private float hintTimer;
    private static PixelToggles instance;
    private Image[] groupImages;
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
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        PixelWindows.Unregister(this);
        if (canvasRoot != null) Destroy(canvasRoot);
    }

    private void Update()
    {
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
            for (int i = 0; i < clicker.Tiers.Length; i++)
            {
                PixelClicker.PixelTier t = clicker.Tiers[i];
                if (!t.CanSwitchOff || !t.unlocked) continue;
                int index = i;
                list.Add(new Entry
                {
                    label = string.Format(pixelLabelFormat, t.displayName),
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
        }
        else
        {
            foreach (PixelMinigame m in PixelMinigame.All)
            {
                if (m == null || !(m.Running || m.UserDisabled)) continue;
                PixelMinigame mg = m;
                list.Add(new Entry
                {
                    label = mg.DisplayName,
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

    private void Refresh()
    {
        for (int i = 0; i < groupImages.Length; i++)
            groupImages[i].color = i == (int)current ? groupActiveColor : groupColor;

        List<Entry> entries = Collect(current);
        while (rows.Count < entries.Count) rows.Add(BuildRow());

        for (int i = 0; i < rows.Count; i++)
        {
            RowUI row = rows[i];
            bool used = i < entries.Count;
            if (row.go.activeSelf != used) row.go.SetActive(used);
            if (!used) continue;

            row.label.text = entries[i].label;
            row.setter = entries[i].setter;
            row.toggle.SetIsOnWithoutNotify(entries[i].on);
            row.go.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -i * (rowHeight + 8f));
        }

        emptyObject.SetActive(entries.Count == 0);
        float contentHeight = entries.Count * (rowHeight + 8f);
        float viewHeight = scroll.GetComponent<RectTransform>().rect.height;
        PixelUIKit.UpdateScrollView(scroll, bar, Mathf.Max(contentHeight - 8f, 0f), viewHeight);
    }

    private RowUI BuildRow()
    {
        RowUI row = new RowUI();
        row.go = new GameObject("Toggle Row", typeof(RectTransform), typeof(Image));
        row.go.transform.SetParent(content, false);
        row.go.GetComponent<Image>().color = rowColor;
        RectTransform rr = row.go.GetComponent<RectTransform>();
        rr.anchorMin = new Vector2(0f, 1f);
        rr.anchorMax = new Vector2(1f, 1f);
        rr.pivot = new Vector2(0.5f, 1f);
        rr.sizeDelta = new Vector2(-24f, rowHeight);

        row.label = PixelUIKit.CreateText(font, row.go.transform, "Label", "", fontSize, TextAlignmentOptions.MidlineLeft,
                                          FontStyles.Normal, textColor);
        row.label.enableAutoSizing = true;
        row.label.fontSizeMax = fontSize;
        row.label.fontSizeMin = 14f;
        RectTransform lr = row.label.rectTransform;
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = Vector2.one;
        lr.offsetMin = new Vector2(16f, 0f);
        lr.offsetMax = new Vector2(-(rowHeight + 20f), 0f);

        GameObject box = new GameObject("Tick Box", typeof(RectTransform), typeof(Image), typeof(Toggle));
        box.transform.SetParent(row.go.transform, false);
        Image bg = box.GetComponent<Image>();
        bg.color = tickBoxColor;
        float size = rowHeight * 0.72f;
        RectTransform br = box.GetComponent<RectTransform>();
        br.anchorMin = br.anchorMax = br.pivot = new Vector2(1f, 0.5f);
        br.sizeDelta = new Vector2(size, size);
        br.anchoredPosition = new Vector2(-14f, 0f);

        GameObject tick = new GameObject("Tick", typeof(RectTransform), typeof(Image));
        tick.transform.SetParent(box.transform, false);
        Image ti = tick.GetComponent<Image>();
        ti.color = tickColor;
        ti.raycastTarget = false;
        RectTransform kr = tick.GetComponent<RectTransform>();
        PixelUIKit.Stretch(kr);
        kr.offsetMin = new Vector2(size * 0.2f, size * 0.2f);
        kr.offsetMax = new Vector2(-size * 0.2f, -size * 0.2f);

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
        hud.Dock(open.GetComponent<RectTransform>(), new Vector2(1f, 0.5f),
                 () => (windowObject != null && windowObject.activeSelf) || (hintObject != null && hintObject.activeSelf)); // stays out while its hint is showing
        open.onClick.AddListener(Toggle);

        // --- The window, just left of the button.
        windowObject = new GameObject("Toggles Window", typeof(RectTransform), typeof(Image));
        windowObject.transform.SetParent(canvasRoot.transform, false);
        windowObject.GetComponent<Image>().color = panelColor;
        RectTransform wr = windowObject.GetComponent<RectTransform>();
        wr.anchorMin = wr.anchorMax = wr.pivot = new Vector2(1f, 0.5f);
        wr.sizeDelta = windowSize;
        wr.anchoredPosition = new Vector2(-(hud.ButtonSize.x + gapToButton + hud.SideMargin * 0.35f), 0f);

        float y = 16f;
        TMP_Text title = PixelUIKit.CreateText(font, windowObject.transform, "Title", windowTitle, titleFontSize,
                                               TextAlignmentOptions.Center, FontStyles.Bold, textColor);
        RectTransform tr = title.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f);
        tr.anchorMax = new Vector2(1f, 1f);
        tr.pivot = new Vector2(0.5f, 1f);
        tr.sizeDelta = new Vector2(-170f, titleFontSize * 1.4f);
        tr.anchoredPosition = new Vector2(0f, -y);

        Button close = PixelUIKit.CreateButton(font, windowObject.transform, "Close", "X", new Vector2(64f, 64f),
                                               new Color(0.3f, 0.3f, 0.35f, 1f), textColor, 34f);
        RectTransform cr = close.GetComponent<RectTransform>();
        cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(1f, 1f);
        cr.anchoredPosition = new Vector2(-14f, -12f);
        close.onClick.AddListener(Close);
        y += titleFontSize * 1.4f + 14f;

        // --- The three group buttons.
        groupImages = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            string name = groupNames != null && i < groupNames.Length ? groupNames[i] : ((Group)i).ToString();
            Button b = PixelUIKit.CreateButton(font, windowObject.transform, "Group " + name, name, Vector2.zero, groupColor,
                                               textColor, fontSize);
            groupImages[i] = b.GetComponent<Image>();
            RectTransform br = b.GetComponent<RectTransform>();
            br.anchorMin = new Vector2(i / 3f, 1f);
            br.anchorMax = new Vector2((i + 1) / 3f, 1f);
            br.pivot = new Vector2(0.5f, 1f);
            br.offsetMin = new Vector2(i == 0 ? 20f : 4f, -(y + 60f));
            br.offsetMax = new Vector2(i == 2 ? -20f : -4f, -y);

            int captured = i;
            b.onClick.AddListener(() =>
            {
                current = (Group)captured;
                content.anchoredPosition = Vector2.zero;
                Refresh();
            });
        }
        y += 60f + 16f;

        // --- The list of switches.
        scroll = PixelUIKit.CreateScrollView(windowObject.transform, "Toggle List", scrollbarColor, 12f, rowHeight, out content, out bar);
        RectTransform vr = scroll.GetComponent<RectTransform>();
        vr.anchorMin = new Vector2(0f, 0f);
        vr.anchorMax = new Vector2(1f, 1f);
        vr.offsetMin = new Vector2(14f, 14f);
        vr.offsetMax = new Vector2(-14f, -y);

        TMP_Text empty = PixelUIKit.CreateText(font, windowObject.transform, "Empty", emptyText, fontSize * 0.9f,
                                               TextAlignmentOptions.Top, FontStyles.Italic,
                                               new Color(textColor.r, textColor.g, textColor.b, 0.6f));
        RectTransform er = empty.rectTransform;
        er.anchorMin = new Vector2(0f, 1f);
        er.anchorMax = new Vector2(1f, 1f);
        er.pivot = new Vector2(0.5f, 1f);
        er.sizeDelta = new Vector2(-40f, fontSize * 1.6f);
        er.anchoredPosition = new Vector2(0f, -(y + 10f));
        emptyObject = empty.gameObject;

        // --- The hint box: sits where the window would, next to the slid-out tab.
        hintObject = new GameObject("Toggles Hint", typeof(RectTransform), typeof(Image));
        hintObject.transform.SetParent(canvasRoot.transform, false);
        hintObject.GetComponent<Image>().color = hintColor;
        RectTransform hr = hintObject.GetComponent<RectTransform>();
        hr.anchorMin = hr.anchorMax = hr.pivot = new Vector2(1f, 0.5f);
        hr.sizeDelta = hintSize;
        hr.anchoredPosition = new Vector2(-(hud.ButtonSize.x + gapToButton + hud.SideMargin * 0.35f), 0f);

        TMP_Text hintLabel = PixelUIKit.CreateText(font, hintObject.transform, "Hint Text", hintText, fontSize * 0.95f,
                                                   TextAlignmentOptions.Center, FontStyles.Normal, textColor);
        RectTransform tr2 = hintLabel.rectTransform;
        tr2.anchorMin = Vector2.zero;
        tr2.anchorMax = Vector2.one;
        tr2.offsetMin = new Vector2(24f, 90f);
        tr2.offsetMax = new Vector2(-24f, -20f);

        Button ok = PixelUIKit.CreateButton(font, hintObject.transform, "Hint Ok", hintOkText, new Vector2(200f, 60f), groupActiveColor,
                                            textColor, fontSize);
        RectTransform okr = ok.GetComponent<RectTransform>();
        okr.anchorMin = okr.anchorMax = okr.pivot = new Vector2(0.5f, 0f);
        okr.anchoredPosition = new Vector2(0f, 20f);
        ok.onClick.AddListener(CloseHint);
        hintObject.SetActive(false);

        windowObject.SetActive(false);
        PixelWindows.Register(this, 15, () => windowObject != null && windowObject.activeSelf, Close);
    }

    private void Toggle()
    {
        if (windowObject.activeSelf) Close();
        else
        {
            CloseHint(); // the player found the window
            windowObject.SetActive(true);
            refreshTimer = 0f;
            Refresh();
        }
    }

    private void Close() => windowObject.SetActive(false);
}
