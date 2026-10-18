// ---- System notes (moved here from CLAUDE.md) ----
// PixelSkillsTracker.cs
//   Part of partial class PixelSkills: the pinned skill tracker. A pin button at the top left of the Skills window (BuildPinButton,
//   green while pinned) closes the window and shows a small draggable window (own canvas sortingOrder - 1 (below the Skills window and
//   its tooltip), dragged by PixelPopoutDrag, which now has a locked flag) with the training skill's name, level and an XP bar
//   (RefreshTracker: TrainingId, LevelOf). Hovering its XP bar shows the XP box (PixelSkills.XpTip: 'Skill: lv/lv', Current XP, Next level, Remainder),
//   like the Skills window's header bar and each card's bar. Left / right arrows either side of the skill name
//   (trackerPrev/Next, CycleTrainedSkill) switch the skill in training through Standard and every available pack. Its buttons: a
//   padlock stick (trackerStuck: can't be dragged), downsize ('-' / '+': a one-line small version) and X (closes it; the Skills menu
//   is NOT reopened). DOUBLE CLICKING the name (small or full; PixelDoubleClick) opens the Skills window on the skill in training with its tree beside it (never the expanded tree). Open / small / stuck / position are PlayerPrefs PixelClicker.Skills.Tracker* (global, not per save); hidden on
//   the title screen and until the Skills button exists (SkillsTabUnlocked: Standard level 2), so Standard alone can be pinned.
//   EARLY TRACKER (trackerBeforeSkills, UpdateTrackerIntro): before that, from the first Standard XP, the full tracker shows at the bottom left (PlaceTrackerBottomLeft: above the
//   bottom bar, right of the event log, trackerIntroMargin) with NO buttons and locked in place (name double click does nothing), so the player watches Standard level up.
//   When Standard level 2's popup appears (SkillsTabUnlocked turns true at that moment) its buttons come back (arrows, lock, downsize, X) and it becomes the normal pinned tracker in that spot.
// ---------------------------------------------------
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The pinned skill tracker (part of <see cref="PixelSkills"/>): the pin button at the top left of the Skills window closes that window and opens a small
/// window you can drag around, showing the skill in training with its level and an XP bar. Its buttons: a lock ('stick': it stays where it is and can't be
/// dragged), a downsize button (a one-line version) and an X. Closing it does NOT bring the Skills window back. Where it was, whether it is small, stuck and
/// open are remembered between sessions (PlayerPrefs, not per save).
/// </summary>
public partial class PixelSkills
{
    private const string TrackerOnPref = "PixelClicker.Skills.TrackerOn", TrackerSmallPref = "PixelClicker.Skills.TrackerSmall",
                         TrackerStuckPref = "PixelClicker.Skills.TrackerStuck", TrackerXPref = "PixelClicker.Skills.TrackerX", TrackerYPref = "PixelClicker.Skills.TrackerY";

    private GameObject trackerCanvas, trackerPanel;
    private RectTransform trackerRect, trackerBarFill;
    private TMP_Text trackerName, trackerXp, trackerPenalty;
    private Image trackerPanelImage, trackerPinImage;
    private bool? trackerPinShownOn;   // the pin's last shown state (null = not set yet)
    private Button trackerClose, trackerSize, trackerStick, trackerPrev, trackerNext;
    private Image trackerSizeImage, trackerStickImage;
    private GameObject trackerShackleClosed, trackerShackleOpen;
    private TMP_Text trackerSizeLabel;
    private PixelPopoutDrag trackerDrag;
    private Vector2 trackerDesired;   // where the player put it; the real position is this pushed back on screen, so resizing never loses it
    private bool trackerOn, trackerSmall, trackerStuck, trackerBuilt;
    private float trackerTimer;

    [Header("Pinned skill tracker")]
    [Tooltip("Before the Skills button exists (Standard level 2): once Standard XP starts coming in, show the tracker at the bottom left (beside the event log) so the player sees the level filling up. It has no buttons and can't be moved; when Standard level 2's popup appears its buttons (arrows, lock, downsize, X) come back and it stays pinned there.")]
    [SerializeField] private bool trackerBeforeSkills = true;
    [Tooltip("Gap between that early tracker and the bottom black bar / the event log (canvas units).")]
    [SerializeField] private float trackerIntroMargin = 16f;
    private bool trackerIntro;   // the early, button-less tracker is showing (see trackerBeforeSkills)

    /// <summary>Is the pinned tracker showing?</summary>
    public bool TrackerOn => trackerOn;

    private void LoadTrackerPrefs()
    {
        trackerOn = PixelPrefs.GetInt(TrackerOnPref, 0) == 1;
        trackerSmall = PixelPrefs.GetInt(TrackerSmallPref, 0) == 1;
        trackerStuck = PixelPrefs.GetInt(TrackerStuckPref, 0) == 1;
    }

    /// <summary>The pin button in the Skills window: close the window and pin the tracker (or, if it is already up, take it away again).</summary>
    private void BuildPinButton()
    {
        Button pin = windowParts.AddHeaderButton("Pin Button", "", true);   // the Skills window's left header slot
        trackerPinImage = pin.GetComponent<Image>();
        PixelUIKit.AddPinIcon(pin, 40f);   // the shared pushpin icon (the Inventory's pin button uses it too)
        pin.onClick.AddListener(PinTracker);
        PixelHoverTip tip = pin.gameObject.AddComponent<PixelHoverTip>();
        System.Func<string> source = () => "Pin your active skill with an XP bar in a small window you can move";
        tip.onEnter = () => hoverTip = source;
        tip.onExit = () => { if (hoverTip == source) hoverTip = null; };
    }

    /// <summary>Double click on the tracker's name (small or full): opens the Skills window on the skill in training with its tree beside it (the normal tree, not the expanded one).</summary>
    private void OpenTrainedSkillWithTree()
    {
        if (PixelTutorialDim.Active || packs == null || packs.Count == 0 || !SkillsTabUnlocked) return;   // (not before the Skills window exists)
        if (!WindowOpen) OpenWindow();
        int index = packs.FindIndex(p => p.id == TrainingId);
        if (index < 0) return;
        selected = index;
        selectedNode = 0;
        treeExpanded = false;
        ApplyTreeExpandGlyph();
        RefreshWindow();
        SkillPack pack = packs[index];
        if (IsAvailable(pack) && pack.nodes != null && pack.nodes.Length > 0)
        {
            PixelPop.Show(treeWindow);
            RefreshWindow();
        }
    }

    private void PinTracker()
    {
        SetTracker(!trackerOn);
        if (trackerOn) PixelPop.Hide(windowObject);   // the Skills window closes and does not come back
    }

    private void SetTracker(bool on)
    {
        trackerOn = on;
        PixelPrefs.SetInt(TrackerOnPref, on ? 1 : 0);
        if (on) { EnsureTracker(); RefreshTracker(); }
        ApplyTrackerVisibility();
    }

    private void EnsureTracker()
    {
        if (trackerBuilt) return;
        trackerBuilt = true;
        trackerCanvas = PixelUIKit.CreateCanvas("PixelSkills Tracker", sortingOrder - 1, new Vector2(1920f, 1080f), true);
        trackerCanvas.transform.SetParent(transform, false);

        trackerPanel = new GameObject("Tracker", typeof(RectTransform), typeof(Image));
        trackerPanel.transform.SetParent(trackerCanvas.transform, false);
        trackerPanelImage = trackerPanel.GetComponent<Image>();
        PixelUIKit.StyleWindow(trackerPanelImage, new Color(0.1f, 0.1f, 0.13f, 0.93f));
        trackerRect = trackerPanel.GetComponent<RectTransform>();
        trackerRect.anchorMin = trackerRect.anchorMax = trackerRect.pivot = new Vector2(0.5f, 0.5f);
        trackerRect.anchoredPosition = new Vector2(PixelPrefs.GetFloat(TrackerXPref, -700f), PixelPrefs.GetFloat(TrackerYPref, 300f));

        trackerDrag = trackerPanel.AddComponent<PixelPopoutDrag>();
        trackerDrag.target = trackerRect;
        trackerDrag.canvas = trackerCanvas.GetComponent<Canvas>();
        trackerDesired = trackerRect.anchoredPosition;
        trackerDrag.onMoved = () => { trackerDesired = trackerRect.anchoredPosition; KeepTrackerOnScreen(); trackerDesired = trackerRect.anchoredPosition; PixelPrefs.SetFloat(TrackerXPref, trackerDesired.x); PixelPrefs.SetFloat(TrackerYPref, trackerDesired.y); };

        trackerName = PixelUIKit.CreateText(Font, trackerPanel.transform, "Name", "", 30f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, textColor);
        trackerName.raycastTarget = true;   // double click the name: open that skill and its tree (drags still reach the panel behind it)
        trackerName.gameObject.AddComponent<PixelDoubleClick>().onDouble = OpenTrainedSkillWithTree;
        trackerName.enableAutoSizing = true;
        trackerName.fontSizeMin = 12f;
        trackerName.alignment = TextAlignmentOptions.Center;   // between the two arrows

        // The hazard XP penalty: a red line just under the tracker while it runs ('XP -20%  0:42').
        trackerPenalty = PixelUIKit.CreateText(Font, trackerPanel.transform, "Penalty", "", 24f, TextAlignmentOptions.Center, FontStyles.Bold, new Color(1f, 0.45f, 0.4f, 1f));
        trackerPenalty.raycastTarget = false;
        RectTransform penaltyRect = trackerPenalty.rectTransform;
        penaltyRect.anchorMin = new Vector2(0f, 0f);
        penaltyRect.anchorMax = new Vector2(1f, 0f);
        penaltyRect.pivot = new Vector2(0.5f, 1f);
        penaltyRect.sizeDelta = new Vector2(0f, 34f);
        penaltyRect.anchoredPosition = new Vector2(0f, -4f);
        trackerPenalty.gameObject.SetActive(false);

        GameObject track = new GameObject("Bar", typeof(RectTransform), typeof(Image));
        track.transform.SetParent(trackerPanel.transform, false);
        Image trackImage = track.GetComponent<Image>();
        trackImage.color = new Color(0.05f, 0.05f, 0.08f, 1f);
        PixelUIKit.StyleBox(trackImage, true);
        trackerBar = track.GetComponent<RectTransform>();
        AddXpTip(track, () => Find(TrainingId));   // hover the tracker's XP bar: the XP box (drags still move the tracker)
        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(track.transform, false);
        fill.GetComponent<Image>().color = new Color(0.3f, 0.8f, 0.45f, 1f);
        fill.GetComponent<Image>().raycastTarget = false;
        trackerBarFill = fill.GetComponent<RectTransform>();
        trackerBarFill.anchorMin = Vector2.zero;
        trackerBarFill.anchorMax = new Vector2(0f, 1f);
        trackerBarFill.pivot = new Vector2(0f, 0.5f);
        trackerBarFill.offsetMin = new Vector2(2f, 2f);
        trackerBarFill.offsetMax = new Vector2(-2f, -2f);

        trackerXp = PixelUIKit.CreateText(Font, trackerPanel.transform, "Xp", "", 22f, TextAlignmentOptions.Center, FontStyles.Bold, textColor);
        trackerXp.raycastTarget = false;

        // Buttons: stick (lock), downsize, close.
        trackerClose = PixelUIKit.CreateButton(Font, trackerPanel.transform, "Close", "X", new Vector2(40f, 40f), ButtonRole.Neutral, 22f);
        trackerClose.onClick.AddListener(() => SetTracker(false));   // does not bring the Skills window back
        trackerSize = PixelUIKit.CreateButton(Font, trackerPanel.transform, "Size", "-", new Vector2(40f, 40f), ButtonRole.Neutral, 26f);
        trackerSizeLabel = trackerSize.GetComponentInChildren<TMP_Text>();
        trackerSizeImage = trackerSize.GetComponent<Image>();
        trackerSize.onClick.AddListener(() =>
        {
            trackerSmall = !trackerSmall;
            PixelPrefs.SetInt(TrackerSmallPref, trackerSmall ? 1 : 0);
            LayoutTracker();
        });
        trackerStick = PixelUIKit.CreateButton(Font, trackerPanel.transform, "Stick", "", new Vector2(40f, 40f), ButtonRole.Neutral, 22f);
        trackerStickImage = trackerStick.GetComponent<Image>();
        trackerShackleClosed = BuildLockPart(trackerStick.transform, false);
        trackerShackleOpen = BuildLockPart(trackerStick.transform, true);
        trackerStick.onClick.AddListener(() =>
        {
            trackerStuck = !trackerStuck;
            PixelPrefs.SetInt(TrackerStuckPref, trackerStuck ? 1 : 0);
            LayoutTracker();
        });
        foreach (Button b in new[] { trackerClose, trackerSize, trackerStick })
        {
            RectTransform r = b.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(1f, 1f);
        }
        // Arrows either side of the skill name: switch the skill in training.
        trackerPrev = PixelUIKit.CreateButton(Font, trackerPanel.transform, "Previous", "<", new Vector2(40f, 40f), ButtonRole.Neutral, 26f);
        PixelUIKit.UseArrowGlyph(trackerPrev, true);
        trackerPrev.onClick.AddListener(() => CycleTrainedSkill(-1));
        trackerNext = PixelUIKit.CreateButton(Font, trackerPanel.transform, "Next", ">", new Vector2(40f, 40f), ButtonRole.Neutral, 26f);
        PixelUIKit.UseArrowGlyph(trackerNext, false);
        trackerNext.onClick.AddListener(() => CycleTrainedSkill(1));
        AddTrackerTip(trackerPrev, () => "Previous skill");
        AddTrackerTip(trackerNext, () => "Next skill");
        AddTrackerTip(trackerClose, () => "Close (the Skills menu stays closed)");
        AddTrackerTip(trackerSize, () => trackerSmall ? "Bigger" : "Downsize");
        AddTrackerTip(trackerStick, () => trackerStuck ? "Stuck in place - click to let it move again" : "Stick it in place so it can't be moved");
        LayoutTracker();
    }

    private RectTransform trackerBar;

    private void AddTrackerTip(Button b, System.Func<string> source)
    {
        PixelHoverTip tip = b.gameObject.AddComponent<PixelHoverTip>();
        tip.onEnter = () => hoverTip = source;
        tip.onExit = () => { if (hoverTip == source) hoverTip = null; };
    }

    /// <summary>A small padlock drawn from rectangles: the body and a shackle (closed = both legs in the body, open = lifted off one side).</summary>
    private GameObject BuildLockPart(Transform parent, bool open)
    {
        GameObject root = new GameObject(open ? "Lock Open" : "Lock Closed", typeof(RectTransform));
        root.transform.SetParent(parent, false);
        RectTransform rr = root.GetComponent<RectTransform>();
        rr.anchorMin = rr.anchorMax = rr.pivot = new Vector2(0.5f, 0.5f);
        rr.sizeDelta = new Vector2(24f, 24f);
        void Part(Vector2 size, Vector2 at, Color c)
        {
            GameObject g = new GameObject("Part", typeof(RectTransform), typeof(Image));
            g.transform.SetParent(root.transform, false);
            g.GetComponent<Image>().color = c;
            g.GetComponent<Image>().raycastTarget = false;
            RectTransform r = g.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = size;
            r.anchoredPosition = at;
        }
        Color metal = new Color(0.85f, 0.88f, 0.95f), body = new Color(0.95f, 0.75f, 0.25f);
        Part(new Vector2(20f, 12f), new Vector2(0f, -5f), body);
        if (!open)
        {
            Part(new Vector2(4f, 10f), new Vector2(-6f, 6f), metal);
            Part(new Vector2(4f, 10f), new Vector2(6f, 6f), metal);
            Part(new Vector2(16f, 4f), new Vector2(0f, 11f), metal);
        }
        else
        {
            Part(new Vector2(4f, 8f), new Vector2(-6f, 5f), metal);
            Part(new Vector2(4f, 10f), new Vector2(6f, 9f), metal);
            Part(new Vector2(16f, 4f), new Vector2(0f, 14f), metal);
        }
        return root;
    }

    private void LayoutTracker()
    {
        if (!trackerBuilt) return;
        bool small = trackerSmall && !trackerIntro;   // (the early tracker is always the full one: the XP numbers are the point)
        foreach (Button b in new[] { trackerClose, trackerSize, trackerStick, trackerPrev, trackerNext })
            if (b.gameObject.activeSelf == trackerIntro) b.gameObject.SetActive(!trackerIntro);   // the early tracker has no buttons
        float w = small ? 300f : 380f, h = small ? 64f : 124f, bs = small ? 28f : 40f;
        trackerRect.sizeDelta = new Vector2(w, h);
        float x = -8f;
        foreach (Button b in new[] { trackerClose, trackerSize, trackerStick })
        {
            RectTransform r = b.GetComponent<RectTransform>();
            r.sizeDelta = new Vector2(bs, bs);
            r.anchoredPosition = new Vector2(x, small ? -6f : -8f);
            x -= bs + 4f;
        }
        float buttonsWidth = trackerIntro ? 8f - bs - 6f : -x + 4f;   // (no buttons: the name gets the whole width)
        RectTransform pr = trackerPrev.GetComponent<RectTransform>(), xr2 = trackerNext.GetComponent<RectTransform>();
        pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(0f, 1f);
        pr.sizeDelta = new Vector2(bs, bs);
        pr.anchoredPosition = new Vector2(8f, small ? -6f : -8f);
        float nameLeft = trackerIntro ? 8f : 8f + bs + 6f;
        xr2.anchorMin = xr2.anchorMax = xr2.pivot = new Vector2(1f, 1f);
        xr2.sizeDelta = new Vector2(bs, bs);
        xr2.anchoredPosition = new Vector2(-buttonsWidth, small ? -6f : -8f);
        RectTransform nr = trackerName.rectTransform;
        nr.anchorMin = new Vector2(0f, 1f);
        nr.anchorMax = new Vector2(1f, 1f);
        nr.pivot = new Vector2(0.5f, 1f);
        nr.offsetMin = new Vector2(nameLeft, small ? -h + 28f : -50f);
        nr.offsetMax = new Vector2(-buttonsWidth - bs - 6f, small ? -4f : -8f);
        trackerName.fontSizeMax = small ? 22f : 30f;
        trackerBar.anchorMin = new Vector2(0f, 0f);
        trackerBar.anchorMax = new Vector2(1f, 0f);
        trackerBar.pivot = new Vector2(0.5f, 0f);
        trackerBar.offsetMin = new Vector2(14f, small ? 8f : 16f);
        trackerBar.offsetMax = new Vector2(-14f, small ? 22f : 52f);
        RectTransform xr = trackerXp.rectTransform;
        xr.anchorMin = trackerBar.anchorMin; xr.anchorMax = trackerBar.anchorMax; xr.pivot = trackerBar.pivot;
        xr.offsetMin = trackerBar.offsetMin; xr.offsetMax = trackerBar.offsetMax;
        trackerXp.gameObject.SetActive(!small);
        PixelUIKit.SetText(trackerSizeLabel, small ? "+" : "-");
        PixelUIKit.SetRole(trackerStickImage, trackerStuck ? ButtonRole.Success : ButtonRole.Neutral);
        trackerShackleClosed.SetActive(trackerStuck);
        trackerShackleOpen.SetActive(!trackerStuck);
        trackerDrag.locked = trackerStuck || trackerIntro;
        KeepTrackerOnScreen();
        RefreshTracker();
    }

    /// <summary>Switches the skill in training to the next (+1) or previous (-1) one you can train (Standard and every available pack).</summary>
    private void CycleTrainedSkill(int direction)
    {
        List<SkillPack> usable = new List<SkillPack>();
        foreach (SkillPack p in packs) if (IsAvailable(p)) usable.Add(p);
        if (usable.Count < 2) return;
        int at = usable.FindIndex(p => p.id == TrainingId);
        int next = ((at < 0 ? 0 : at) + direction + usable.Count) % usable.Count;
        SetActive(usable[next].id);
        RefreshTracker();
    }

    private void KeepTrackerOnScreen()
    {
        if (!trackerBuilt) return;
        RectTransform canvasRect = trackerCanvas.GetComponent<RectTransform>();
        float bars = PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f;
        Vector2 half = canvasRect.rect.size * 0.5f, size = trackerRect.sizeDelta * 0.5f;
        Vector2 p = trackerDesired;
        p.x = Mathf.Clamp(p.x, -half.x + size.x, half.x - size.x);
        p.y = Mathf.Clamp(p.y, -half.y + bars + size.y, half.y - bars - size.y);
        trackerRect.anchoredPosition = p;
    }

    private void RefreshTracker()
    {
        if (!trackerBuilt) return;
        SkillPack pack = Find(TrainingId);
        if (pack == null) return;
        {
            int level = LevelOf(pack, out double into, out double need);
            PixelUIKit.SetText(trackerName, pack.id == StandardId && StandardHidden ? ShownName(pack) : pack.displayName + "   Lv " + level);   // (the mystery bar: just '???')
            double bonus = Bonus(pack);
            PixelUIKit.SetText(trackerXp, PixelClicker.FormatNumberShort(into) + " / " + PixelClicker.FormatNumberShort(need) + " XP"
                + (bonus >= 1d ? "   <color=#ffd54a>Bonus " + PixelClicker.FormatNumberShort(bonus) + "</color>" : ""));
            trackerBarFill.anchorMax = new Vector2(Mathf.Clamp01((float)(into / System.Math.Max(1d, need))), 1f);
        }
        trackerBarFill.offsetMax = new Vector2(-2f, -2f);

        bool penalised = PenaltyPercent(pack.id) > 0;   // only the skill it hit shows it
        if (trackerPenalty != null)
        {
            if (trackerPenalty.gameObject.activeSelf != penalised) trackerPenalty.gameObject.SetActive(penalised);
            if (penalised)
            {
                int left = Mathf.CeilToInt(PenaltySecondsLeft(pack.id));
                PixelUIKit.SetText(trackerPenalty, "XP -" + PenaltyPercent(pack.id) + "%   " + (left / 60) + ":" + (left % 60).ToString("00"));
            }
        }
    }

    private void ApplyTrackerVisibility()
    {
        bool show = trackerBuilt && !PixelTitleScreen.Showing && (trackerIntro || (trackerOn && SkillsTabUnlocked));   // (from Standard level 2, like the Skills button; before that only the early tracker)
        if (trackerCanvas != null && trackerCanvas.activeSelf != show) trackerCanvas.SetActive(show);
        if (trackerPinImage != null && trackerPinShownOn != trackerOn)   // (runs every frame: only re-role on a change)
        {
            trackerPinShownOn = trackerOn;
            PixelUIKit.SetRole(trackerPinImage, trackerOn ? ButtonRole.Success : ButtonRole.Neutral);
        }
    }

    /// <summary>
    /// The early tracker (trackerBeforeSkills): from the first Standard XP until Standard level 2's popup appears (that is when SkillsTabUnlocked turns true),
    /// a button-less, unmovable tracker sits at the bottom left beside the event log. When the popup appears it becomes the normal pinned tracker, in the same spot.
    /// </summary>
    private void UpdateTrackerIntro()
    {
        bool unlocked = SkillsTabUnlocked;
        bool want = trackerBeforeSkills && !unlocked && clicker != null && Xp(Find(StandardId)) > 0d;
        if (want == trackerIntro)
        {
            if (trackerIntro) PlaceTrackerBottomLeft();   // (follows the event log being switched on / off)
            return;
        }
        trackerIntro = want;
        if (want) { EnsureTracker(); PlaceTrackerBottomLeft(); LayoutTracker(); return; }
        if (!unlocked) { LayoutTracker(); return; }   // (switched off in the Inspector: just gone)
        // Standard level 2's popup is up: keep it pinned where it is, with its buttons back.
        PixelPrefs.SetFloat(TrackerXPref, trackerDesired.x);
        PixelPrefs.SetFloat(TrackerYPref, trackerDesired.y);
        SetTracker(true);
        LayoutTracker();
    }

    /// <summary>The early tracker's spot: bottom left, above the bottom bar and right of the event log (or the screen edge when the log is off).</summary>
    private void PlaceTrackerBottomLeft()
    {
        RectTransform canvasRect = trackerCanvas.GetComponent<RectTransform>();
        float bars = PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f;
        float margin = Mathf.Max(0f, trackerIntroMargin);
        float left = PixelHints.EventLogEnabled ? PixelHints.LogRightEdge : margin;
        Vector2 half = canvasRect.rect.size * 0.5f, size = trackerRect.sizeDelta * 0.5f;
        trackerDesired = new Vector2(-half.x + left + size.x, -half.y + bars + margin + size.y);
        KeepTrackerOnScreen();
    }

    private void UpdateTracker()
    {
        if (!built) return;
        UpdateTrackerIntro();
        if (!trackerBuilt && trackerOn && SkillsTabUnlocked) { EnsureTracker(); }
        ApplyTrackerVisibility();
        if (!trackerBuilt || !(trackerOn || trackerIntro)) return;
        UpdateReveal();   // (Standard's level 2 reveal: the punch and gold flash)
        trackerTimer -= Time.unscaledDeltaTime;
        if (trackerTimer > 0f) return;
        trackerTimer = 0.25f;
        RefreshTracker();
        KeepTrackerOnScreen();
    }
}

/// <summary>Calls 'onDouble' on a double click (a double click on a UI element that is not draggable itself: drags go to the parent that handles them).</summary>
public class PixelDoubleClick : MonoBehaviour, UnityEngine.EventSystems.IPointerClickHandler
{
    public System.Action onDouble;
    private float lastClick = -10f;

    public void OnPointerClick(UnityEngine.EventSystems.PointerEventData e)
    {
        bool dbl = e.clickCount >= 2 || Time.unscaledTime - lastClick < 0.35f;
        lastClick = dbl ? -10f : Time.unscaledTime;
        if (dbl) onDouble?.Invoke();
    }
}
