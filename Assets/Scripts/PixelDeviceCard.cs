// ---- System notes ----
// PixelDeviceCard.cs
//   The ONE info card + panel for placed devices (design agreed in the Design Discussion chat, 2026-10-18; doc: docs/systems/PixelFactory.md 'Device card').
//   * HOVER a placed device for hoverDelay (0.5 s): a small card beside the pointer (PixelUIKit.PlaceTooltip, the hover-tooltip spot) with the
//     device's essentials - its name, time / uses left as a bar, its 2-3 key stats, contents like 6 / 10. Hidden while the block / husk card is wanted
//     (PixelBlockTooltip.HoveringBlock: a husk in the bin shows the husk's card), over a part that claimed the pointer (the Recycler's button, the
//     hopper's flag: PixelPointerClaim.ButtonClaimed), while carrying, in a menu, in first person.
//   * RIGHT-CLICK (a TAP: released within tapSeconds) a placed device: a small Tool window (PixelUIKit.CreateWindow, the theme) with the full sheet
//     (run time, reach, Operation levels, what it did this session) and actions: Remove (Danger; asks once: 'Sure?') and Upgrades (opens the shop's
//     Upgrades > Operation tab on that device: PixelShop.OpenOperationFor; shown only for devices with an Operation row). A right HOLD still removes
//     the device (PixelConsumables.UpdateRemoval) - the tap just never fills that ring. Who owns the right click is PixelRightClick's Device owner.
//     Escape (PixelWindows) / a click anywhere outside the panel / the device going away closes it.
//   * STEPPERS: '- value +' rows (PixelDeviceInfo.AddStepper) for the Operation upgrades' level in use (PixelShop.AddOperationLevels): turn a bought
//     upgrade back down (PlaceSteppers).
//   * TICK BOXES (2026-10-20): 'on / off' rows (PixelDeviceInfo.AddToggle, PlaceToggles), e.g. the Sorting Hopper's outputs.
//   * DEVICE ACTIONS: buttons a device adds itself (PixelDeviceInfo.AddAction, e.g. the Sorting Hopper's Select filter / Empty out) sit two to a row
//     above Remove / Upgrades (PlaceActions).
//   * NOT ONLY DEVICES (2026-10-20): OpenSubject(IPixelPanelSubject) shows the same panel for something that is not a placed device - a conveyor belt
//     SECTION (PixelConveyorPanel: a right tap on a belt; the section glows while the panel is open). The subject fills the sheet and says what Remove does.
//   * LEFT-CLICK is untouched (each device's own button, flag, winding...).
//   The lines come from the device itself: PixelPlacedDevice.BuildInfo (common lines + AddInfo + IPixelDeviceInfo components; PixelDeviceInfo.cs).
//   The panel then adds one row per husk socket (PixelCouplings.AddPortRows: what each port is joined to, from the connection resolver).
//   Refreshed every refreshSeconds while shown (no per-frame text building).
// ---------------------------------------------------
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PixelDeviceCard : MonoBehaviour
{
    [Header("Hover card")]
    [Tooltip("Seconds the pointer must rest on a placed device before its card shows.")]
    [SerializeField] private float hoverDelay = 0.5f;
    [Tooltip("Width of the hover card (canvas units, 1920 x 1080 reference).")]
    [SerializeField] private float cardWidth = 400f;
    [Tooltip("Seconds between refreshes of the card / panel text while shown.")]
    [SerializeField] private float refreshSeconds = 0.25f;
    [Tooltip("Font size of the card's title (the lines are 0.8 of it).")]
    [SerializeField] private float titleSize = 30f;

    [Header("Right-click panel")]
    [Tooltip("A right press released within this many seconds is a TAP and opens the panel (holding longer is the old hold-to-remove).")]
    [SerializeField] private float tapSeconds = 0.3f;
    [Tooltip("Width of the panel.")]
    [SerializeField] private float panelWidth = 520f;
    [Tooltip("Label of the panel's Remove button.")]
    [SerializeField] private string removeLabel = "Remove";
    [Tooltip("What the Remove button says after the first click (a second click removes the device).")]
    [SerializeField] private string removeConfirmLabel = "Sure?";
    [Tooltip("Label of the panel's Upgrades button (opens the shop's Operation tab on this device).")]
    [SerializeField] private string upgradesLabel = "Upgrades";
    [Tooltip("Shown in the panel under the lines: how the device is removed without the panel.")]
    [SerializeField] private string panelHint = "Hold the right button on a device to remove it straight away.";
    [Tooltip("Sorting order of the card / panel canvas (the block tooltip is 655).")]
    [SerializeField] private int sortingOrder = 650;

    private PixelClicker clicker;
    private PixelConsumables consumables;
    private GameObject canvasGo;
    private RectTransform canvasRect;
    private readonly PixelDeviceInfo info = new PixelDeviceInfo();

    // Hover card
    private RectTransform card;
    private CanvasGroup cardGroup;
    private TMP_Text cardTitle, cardLines;
    private Bar cardBar;
    private PixelPlacedDevice hovered, cardDevice;
    private float hoverTime, cardAlpha, nextCardRefresh;

    // Panel
    private PixelWindowParts panel;
    private TMP_Text panelLines, panelHintText;
    private Bar panelBar;
    private Button removeButton, upgradesButton;
    private TMP_Text removeText;
    private PixelPlacedDevice panelDevice;
    private IPixelPanelSubject panelSubject;   // (or something that is not a placed device: a conveyor belt section)
    private float nextPanelRefresh, removeArmedUntil;

    /// <summary>Is the panel showing a target that still exists?</summary>
    private bool PanelTargetAlive => panelSubject != null ? panelSubject.PanelAlive : panelDevice != null && !panelDevice.IsEnding;

    // Right tap
    private PixelPlacedDevice pressDevice;
    private float pressTime;
    private bool rightWasHeld;

    private sealed class Bar { public RectTransform root; public Image fill; public TMP_Text label; }

    private void Awake()
    {
        clicker = PixelFind.Cached<PixelClicker>();
        PixelWindows.Register(this, 45, () => (panelDevice != null || panelSubject != null) && panel != null && PixelPop.IsOpen(panel.Root), ClosePanel);
    }

    private void OnDestroy()
    {
        PixelWindows.Unregister(this);
        if (canvasGo != null) Destroy(canvasGo);
    }

    private void Update()
    {
        if (clicker == null) { clicker = PixelFind.Cached<PixelClicker>(); return; }
        if (consumables == null) consumables = PixelFind.Cached<PixelConsumables>();

        bool calm = !PixelCalm.InMenu && !PixelFirstPerson.Active;
        PixelPlacedDevice under = calm && PixelRightClick.Current == PixelRightClick.Owner.Device ? PixelRightClick.Device : null;

        UpdateRightTap(under);
        UpdatePanel(calm);
        UpdateCard(under);
    }

    // ------------------------------------------------------------------
    // Right tap -> panel
    // ------------------------------------------------------------------

    private void UpdateRightTap(PixelPlacedDevice under)
    {
        bool held = PixelInput.RightHeld();
        if (PixelInput.RightPressed()) { pressDevice = under; pressTime = Time.unscaledTime; }
        else if (rightWasHeld && !held)
        {
            // Released: a short press on the same device opens its panel.
            if (pressDevice != null && pressDevice == under && !pressDevice.IsEnding && Time.unscaledTime - pressTime <= tapSeconds) OpenPanel(pressDevice);
            pressDevice = null;
        }
        rightWasHeld = held;
    }

    /// <summary>Opens the shared panel on something that is not a placed device (a conveyor belt section: PixelConveyorPanel).</summary>
    public static void OpenSubject(IPixelPanelSubject subject)
    {
        PixelDeviceCard card = PixelFind.Cached<PixelDeviceCard>();
        if (card == null || subject == null || card.clicker == null) return;
        card.OpenPanel(null, subject);
    }

    private void OpenPanel(PixelPlacedDevice device, IPixelPanelSubject subject = null)
    {
        EnsureCanvas();
        if (panel == null) BuildPanel();
        if (panelSubject != null && panelSubject != subject) panelSubject.PanelClosed();
        panelDevice = subject != null ? null : device;
        panelSubject = subject;
        if (subject != null) subject.PanelOpened();
        removeArmedUntil = 0f;
        nextPanelRefresh = 0f;
        if (!PixelPop.IsOpen(panel.Root) && panel.Chrome != null) panel.Chrome.PlaceNow();
        PixelPop.Show(panel.Root);
        if (panel.Chrome != null) panel.Chrome.Wake();
        RefreshPanel();
        hoverTime = 0f;   // (the card steps aside for the panel)
    }

    private void ClosePanel()
    {
        panelDevice = null;
        if (panelSubject != null) { IPixelPanelSubject was = panelSubject; panelSubject = null; was.PanelClosed(); }
        if (panel != null) PixelPop.Hide(panel.Root);
    }

    private void UpdatePanel(bool calm)
    {
        if (panel == null || !PixelPop.IsOpen(panel.Root)) { if (panelDevice != null || panelSubject != null) ClosePanel(); return; }
        if (!PanelTargetAlive || !calm) { ClosePanel(); return; }   // (the device / belt was destroyed)
        // A click anywhere outside the panel closes it (a right tap on another device reopens it on release).
        if ((PixelInput.LeftPressed() || PixelInput.RightPressed()) && !RectTransformUtility.RectangleContainsScreenPoint(panel.Rect, PixelInput.PointerPosition(), null))
        {
            ClosePanel();
            return;
        }
        if (removeArmedUntil > 0f && Time.unscaledTime > removeArmedUntil) { removeArmedUntil = 0f; removeText.text = removeLabel; }
        if (Time.unscaledTime >= nextPanelRefresh) RefreshPanel();
    }

    private void RefreshPanel()
    {
        nextPanelRefresh = Time.unscaledTime + refreshSeconds;
        if (panel.Chrome != null && panel.Chrome.Minimized) return;   // (minimized: only the header shows; the rows are laid out again when it opens up)
        info.Clear();
        info.Full = true;
        if (panelSubject != null) panelSubject.BuildPanelInfo(info);
        else { panelDevice.BuildInfo(info); PixelCouplings.AddPortRows(info, panelDevice.GetComponent<IPixelSocketDevice>()); }
        panel.Title.text = info.Title.ToUpperInvariant();
        float pad = 18f, w = panelWidth - pad * 2f, y = panel.HeaderHeight + 10f;
        panelLines.text = string.Join("\n", info.Lines);
        float h = panelLines.GetPreferredValues(panelLines.text, w, 0f).y;
        Place(panelLines.rectTransform, pad, y, w, h); y += h + 12f;
        y = PlaceBar(panelBar, pad, y, w);
        float hh = panelHintText.GetPreferredValues(panelHint, w, 0f).y;
        Place(panelHintText.rectTransform, pad, y, w, hh); y += hh + 12f;
        y = PlaceSteppers(pad, y, w);
        y = PlaceToggles(pad, y, w);
        y = PlaceActions(pad, y, w);

        PixelConsumables.DeviceKind? kind = panelDevice != null ? panelDevice.Kind : null;
        PixelShop shop = PixelFind.Cached<PixelShop>();
        bool upgrades = kind.HasValue && shop != null && shop.HasOperationRow(kind.Value);
        upgradesButton.gameObject.SetActive(upgrades);
        float bw = upgrades ? (w - 12f) * 0.5f : w;
        Place(removeButton.GetComponent<RectTransform>(), pad, y, bw, 56f);
        if (upgrades) Place(upgradesButton.GetComponent<RectTransform>(), pad + bw + 12f, y, bw, 56f);
        y += 56f + pad;
        panel.SetHeight(y);
    }

    private readonly List<Button> actionButtons = new List<Button>();
    private readonly List<PixelDeviceInfo.DeviceAction> panelActions = new List<PixelDeviceInfo.DeviceAction>();

    /// <summary>The device's own buttons (info.Actions), two to a row (a last odd one full width); returns the y under them.</summary>
    private float PlaceActions(float pad, float y, float w)
    {
        panelActions.Clear();
        panelActions.AddRange(info.Actions);
        float bw = (w - 12f) * 0.5f;
        for (int i = 0; i < panelActions.Count; i++)
        {
            if (i >= actionButtons.Count)
            {
                int index = i;
                Button made = PixelUIKit.CreateButton(clicker.UIFont, panel.Root.transform, "Action " + i, "", new Vector2(200f, 56f), ButtonRole.Primary, 26f);
                made.onClick.AddListener(() => RunAction(index));
                actionButtons.Add(made);
            }
            Button b = actionButtons[i];
            if (!b.gameObject.activeSelf) b.gameObject.SetActive(true);
            TMP_Text label = b.GetComponentInChildren<TMP_Text>();
            if (label.text != panelActions[i].Label) label.text = panelActions[i].Label;
            PixelUIKit.SetRole(b, panelActions[i].Role);
            bool alone = i == panelActions.Count - 1 && i % 2 == 0;
            Place(b.GetComponent<RectTransform>(), pad + (i % 2) * (bw + 12f), y + (i / 2) * 66f, alone ? w : bw, 56f);
        }
        for (int i = panelActions.Count; i < actionButtons.Count; i++) if (actionButtons[i].gameObject.activeSelf) actionButtons[i].gameObject.SetActive(false);
        return y + (panelActions.Count + 1) / 2 * 66f;
    }

    private sealed class StepperRow { public RectTransform root; public TMP_Text label, value; public Button minus, plus; }
    private readonly List<StepperRow> stepperRows = new List<StepperRow>();
    private readonly List<PixelDeviceInfo.DeviceStepper> panelSteppers = new List<PixelDeviceInfo.DeviceStepper>();
    private const float StepperH = 50f;

    /// <summary>The '- value +' rows (info.Steppers: the Operation upgrades' level in use), one per row; returns the y under them.</summary>
    private float PlaceSteppers(float pad, float y, float w)
    {
        panelSteppers.Clear();
        panelSteppers.AddRange(info.Steppers);
        for (int i = 0; i < panelSteppers.Count; i++)
        {
            if (i >= stepperRows.Count) stepperRows.Add(MakeStepperRow(i));
            StepperRow row = stepperRows[i];
            PixelDeviceInfo.DeviceStepper st = panelSteppers[i];
            if (!row.root.gameObject.activeSelf) row.root.gameObject.SetActive(true);
            Place(row.root, pad, y + i * (StepperH + 6f), w, StepperH);
            if (row.label.text != st.Label) row.label.text = st.Label;
            if (row.value.text != st.Value) row.value.text = st.Value;
            row.minus.interactable = st.OnMinus != null;
            row.plus.interactable = st.OnPlus != null;
        }
        for (int i = panelSteppers.Count; i < stepperRows.Count; i++) if (stepperRows[i].root.gameObject.activeSelf) stepperRows[i].root.gameObject.SetActive(false);
        return y + panelSteppers.Count * (StepperH + 6f) + (panelSteppers.Count > 0 ? 6f : 0f);
    }

    private StepperRow MakeStepperRow(int index)
    {
        TMP_FontAsset font = clicker.UIFont;
        StepperRow row = new StepperRow();
        GameObject go = new GameObject("Stepper " + index, typeof(RectTransform));
        go.transform.SetParent(panel.Root.transform, false);
        row.root = go.GetComponent<RectTransform>();
        float size = titleSize * 0.8f;
        row.label = PixelUIKit.CreateText(font, go.transform, "Label", "", size, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, PixelUITheme.TextColor);
        RectTransform lr = row.label.rectTransform;
        lr.anchorMin = new Vector2(0f, 0f); lr.anchorMax = new Vector2(0.4f, 1f); lr.offsetMin = lr.offsetMax = Vector2.zero;
        row.minus = PixelUIKit.CreateButton(font, go.transform, "Minus", "-", new Vector2(StepperH, StepperH), ButtonRole.Neutral, 30f);
        RectTransform mr = row.minus.GetComponent<RectTransform>();
        mr.anchorMin = mr.anchorMax = mr.pivot = new Vector2(0.4f, 0.5f); mr.anchoredPosition = new Vector2(StepperH * 0.5f, 0f);
        row.plus = PixelUIKit.CreateButton(font, go.transform, "Plus", "+", new Vector2(StepperH, StepperH), ButtonRole.Neutral, 30f);
        RectTransform pr = row.plus.GetComponent<RectTransform>();
        pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(1f, 0.5f); pr.anchoredPosition = Vector2.zero;
        row.value = PixelUIKit.CreateText(font, go.transform, "Value", "", size, TextAlignmentOptions.Center, FontStyles.Normal, PixelUITheme.TextColor);
        RectTransform vr = row.value.rectTransform;
        vr.anchorMin = new Vector2(0.4f, 0f); vr.anchorMax = new Vector2(1f, 1f); vr.offsetMin = new Vector2(StepperH + 6f, 0f); vr.offsetMax = new Vector2(-StepperH - 6f, 0f);
        row.minus.onClick.AddListener(() => RunStepper(index, false));
        row.plus.onClick.AddListener(() => RunStepper(index, true));
        return row;
    }

    private sealed class ToggleRow { public RectTransform root; public Toggle box; public TMP_Text label; }
    private readonly List<ToggleRow> toggleRows = new List<ToggleRow>();
    private readonly List<PixelDeviceInfo.DeviceToggle> panelToggles = new List<PixelDeviceInfo.DeviceToggle>();
    private const float ToggleH = 44f;

    /// <summary>The tick box rows (info.Toggles), one per row; returns the y under them.</summary>
    private float PlaceToggles(float pad, float y, float w)
    {
        panelToggles.Clear();
        panelToggles.AddRange(info.Toggles);
        for (int i = 0; i < panelToggles.Count; i++)
        {
            if (i >= toggleRows.Count) toggleRows.Add(MakeToggleRow(i));
            ToggleRow row = toggleRows[i];
            if (!row.root.gameObject.activeSelf) row.root.gameObject.SetActive(true);
            Place(row.root, pad, y + i * (ToggleH + 4f), w, ToggleH);
            if (row.label.text != panelToggles[i].Label) row.label.text = panelToggles[i].Label;
            if (row.box.isOn != panelToggles[i].On) row.box.SetIsOnWithoutNotify(panelToggles[i].On);
        }
        for (int i = panelToggles.Count; i < toggleRows.Count; i++) if (toggleRows[i].root.gameObject.activeSelf) toggleRows[i].root.gameObject.SetActive(false);
        return y + panelToggles.Count * (ToggleH + 4f) + (panelToggles.Count > 0 ? 8f : 0f);
    }

    private ToggleRow MakeToggleRow(int index)
    {
        ToggleRow row = new ToggleRow();
        GameObject go = new GameObject("Toggle " + index, typeof(RectTransform));
        go.transform.SetParent(panel.Root.transform, false);
        row.root = go.GetComponent<RectTransform>();
        float box = ToggleH * 0.8f;
        row.box = PixelUIKit.CreateTickBox(go.transform, box, new Color(0f, 0f, 0f, 0.45f), PixelUITheme.HighlightColor);
        RectTransform br = row.box.GetComponent<RectTransform>();
        br.anchorMin = br.anchorMax = br.pivot = new Vector2(0f, 0.5f);
        br.anchoredPosition = new Vector2(2f, 0f);
        row.label = PixelUIKit.CreateText(clicker.UIFont, go.transform, "Label", "", titleSize * 0.8f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, PixelUITheme.TextColor);
        row.label.richText = true;
        RectTransform lr = row.label.rectTransform;
        lr.anchorMin = new Vector2(0f, 0f); lr.anchorMax = new Vector2(1f, 1f); lr.offsetMin = new Vector2(box + 14f, 0f); lr.offsetMax = Vector2.zero;
        row.box.onValueChanged.AddListener(on => RunToggle(index, on));
        return row;
    }

    private void RunToggle(int index, bool on)
    {
        if (!PanelTargetAlive || index >= panelToggles.Count) return;
        panelToggles[index].OnChange?.Invoke(on);
        if (PanelTargetAlive && panel != null && PixelPop.IsOpen(panel.Root)) RefreshPanel();
    }

    private void RunStepper(int index, bool plus)
    {
        if (!PanelTargetAlive || index >= panelSteppers.Count) return;
        System.Action act = plus ? panelSteppers[index].OnPlus : panelSteppers[index].OnMinus;
        act?.Invoke();
        if (PanelTargetAlive && panel != null && PixelPop.IsOpen(panel.Root)) RefreshPanel();
    }

    private void RunAction(int index)
    {
        if (!PanelTargetAlive || index >= panelActions.Count) return;
        panelActions[index].OnClick?.Invoke();
        if (PanelTargetAlive && panel != null && PixelPop.IsOpen(panel.Root)) RefreshPanel();
        else if (panel != null && PixelPop.IsOpen(panel.Root)) ClosePanel();
    }

    private void BuildPanel()
    {
        TMP_FontAsset font = clicker.UIFont;
        panel = PixelUIKit.CreateWindow(font, canvasGo.transform, "Device Panel", "Device", new Vector2(panelWidth, 300f), PixelUIKit.WindowKind.Tool,
                                        ClosePanel, "PixelClicker.Window.DevicePanel");
        if (panel.Chrome != null)
        {
            panel.Chrome.defaultPlacer = cs => new Vector2(cs.x * 0.62f, -cs.y * 0.22f);
            panel.Chrome.StateChanged += () => nextPanelRefresh = 0f;   // (opened up again: re-lay the rows at once)
        }
        panelLines = Text(panel.Root.transform, "Lines", titleSize * 0.8f, PixelUITheme.TextColor);
        panelBar = MakeBar(panel.Root.transform);
        panelHintText = Text(panel.Root.transform, "Hint", titleSize * 0.62f, PixelUITheme.DimTextColor);
        panelHintText.text = panelHint;
        removeButton = PixelUIKit.CreateButton(font, panel.Root.transform, "Remove", removeLabel, new Vector2(200f, 56f), ButtonRole.Danger, 26f);
        removeText = removeButton.GetComponentInChildren<TMP_Text>();
        removeButton.onClick.AddListener(OnRemove);
        upgradesButton = PixelUIKit.CreateButton(font, panel.Root.transform, "Upgrades", upgradesLabel, new Vector2(200f, 56f), ButtonRole.Primary, 26f);
        upgradesButton.onClick.AddListener(OnUpgrades);
        panel.Root.SetActive(false);
    }

    private void OnRemove()
    {
        if (!PanelTargetAlive) return;
        if (removeArmedUntil <= 0f) { removeArmedUntil = Time.unscaledTime + 2.5f; removeText.text = removeConfirmLabel; return; }
        if (panelSubject != null) panelSubject.PanelRemove();
        else if (consumables != null) consumables.RemoveDevice(panelDevice); else panelDevice.RemoveNow();
        ClosePanel();
    }

    private void OnUpgrades()
    {
        if (panelDevice == null) return;
        PixelConsumables.DeviceKind? kind = panelDevice.Kind;
        PixelShop shop = PixelFind.Cached<PixelShop>();
        ClosePanel();
        if (kind.HasValue && shop != null) shop.OpenOperationFor(kind.Value);
    }

    // ------------------------------------------------------------------
    // Hover card
    // ------------------------------------------------------------------

    private void UpdateCard(PixelPlacedDevice under)
    {
        bool blocked = under == null || under == panelDevice || PixelBlockTooltip.HoveringBlock || PixelPointerClaim.ButtonClaimed || PixelCarryHandles.Hot
                       || PixelInput.RightHeld() || under.IsEnding;
        PixelPlacedDevice want = blocked ? null : under;
        if (want != hovered) { hovered = want; hoverTime = 0f; }
        if (hovered != null) hoverTime += Time.unscaledDeltaTime;
        bool show = hovered != null && hoverTime >= hoverDelay;
        if (show && card == null) { EnsureCanvas(); BuildCard(); }
        if (card == null) return;

        cardAlpha = Mathf.MoveTowards(cardAlpha, show ? 1f : 0f, Time.unscaledDeltaTime * (show ? 8f : 12f));
        if (cardAlpha <= 0f) { if (card.gameObject.activeSelf) card.gameObject.SetActive(false); cardDevice = null; return; }
        if (!card.gameObject.activeSelf) card.gameObject.SetActive(true);
        cardGroup.alpha = cardAlpha;
        if (show && (hovered != cardDevice || Time.unscaledTime >= nextCardRefresh)) { cardDevice = hovered; RefreshCard(); }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, PixelInput.PointerPosition(), null, out Vector2 local);
        PixelUIKit.PlaceTooltip(card, canvasRect, local);
    }

    private void RefreshCard()
    {
        nextCardRefresh = Time.unscaledTime + refreshSeconds;
        if (cardDevice == null) return;
        info.Clear();
        info.Full = false;
        cardDevice.BuildInfo(info);
        float pad = 16f, w = cardWidth - pad * 2f, y = pad;
        cardTitle.text = info.Title;
        Place(cardTitle.rectTransform, pad, y, w, titleSize * 1.3f); y += titleSize * 1.3f + 4f;
        cardLines.text = string.Join("\n", info.Lines);
        float h = info.Lines.Count > 0 ? cardLines.GetPreferredValues(cardLines.text, w, 0f).y : 0f;
        Place(cardLines.rectTransform, pad, y, w, h); y += h + (h > 0f ? 10f : 0f);
        y = PlaceBar(cardBar, pad, y, w);
        card.sizeDelta = new Vector2(cardWidth, y + pad - 4f);
    }

    private void BuildCard()
    {
        GameObject go = new GameObject("Device Card", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        go.transform.SetParent(canvasGo.transform, false);
        card = go.GetComponent<RectTransform>();
        card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
        Image bg = go.GetComponent<Image>();
        PixelUIKit.StyleWindow(bg, WindowShade.ToolWindow);
        bg.raycastTarget = false;
        cardGroup = go.GetComponent<CanvasGroup>();
        cardGroup.blocksRaycasts = false;
        cardGroup.interactable = false;
        cardTitle = Text(card, "Title", titleSize, PixelUITheme.TitleColor);
        cardTitle.fontStyle = FontStyles.Bold;
        cardLines = Text(card, "Lines", titleSize * 0.75f, PixelUITheme.TextColor);
        cardBar = MakeBar(card);
        go.SetActive(false);
    }

    // ------------------------------------------------------------------
    // Shared pieces
    // ------------------------------------------------------------------

    private void EnsureCanvas()
    {
        if (canvasGo != null) return;
        canvasGo = PixelUIKit.CreateCanvas("Device Card Canvas", sortingOrder, new Vector2(1920f, 1080f), true);
        canvasRect = canvasGo.GetComponent<RectTransform>();
    }

    private TMP_Text Text(Transform parent, string objectName, float size, Color color)
    {
        TMP_Text t = PixelUIKit.CreateText(clicker.UIFont, parent, objectName, "", size, TextAlignmentOptions.TopLeft, FontStyles.Normal, color);
        t.raycastTarget = false;
        t.lineSpacing = 8f;
        return t;
    }

    private Bar MakeBar(Transform parent)
    {
        Bar b = new Bar();
        GameObject go = new GameObject("Bar", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        b.root = go.GetComponent<RectTransform>();
        Image bg = go.GetComponent<Image>();
        PixelUIKit.StyleBox(bg, true);
        bg.raycastTarget = false;
        GameObject f = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        f.transform.SetParent(go.transform, false);
        b.fill = f.GetComponent<Image>();
        b.fill.color = PixelUITheme.HighlightColor;
        b.fill.raycastTarget = false;
        RectTransform fr = b.fill.rectTransform;
        fr.anchorMin = new Vector2(0f, 0f); fr.anchorMax = new Vector2(1f, 1f);
        fr.offsetMin = new Vector2(3f, 3f); fr.offsetMax = new Vector2(-3f, -3f);
        b.label = PixelUIKit.CreateText(clicker.UIFont, go.transform, "Label", "", 20f, TextAlignmentOptions.Center, FontStyles.Bold, Color.white);
        b.label.raycastTarget = false;
        PixelUIKit.Stretch(b.label.rectTransform);
        PixelUIKit.FitOneLine(b.label, 11f);   // (a long label shrinks to one line inside the bar instead of spilling over it)
        return b;
    }

    /// <summary>Lays out the bar at y (top-down) when the sheet has progress; returns the y below it.</summary>
    private float PlaceBar(Bar b, float x, float y, float w)
    {
        bool on = info.Progress >= 0f;
        if (b.root.gameObject.activeSelf != on) b.root.gameObject.SetActive(on);
        if (!on) return y;
        Place(b.root, x, y, w, 30f);
        RectTransform fr = b.fill.rectTransform;
        fr.anchorMax = new Vector2(Mathf.Clamp01(info.Progress), 1f);
        b.label.text = info.ProgressLabel;
        return y + 30f + 10f;
    }

    /// <summary>Top-left placement inside a parent (x, y from its top-left corner, y downwards).</summary>
    private static void Place(RectTransform r, float x, float y, float w, float h)
    {
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = new Vector2(x, -y);
        r.sizeDelta = new Vector2(w, h);
    }
}
