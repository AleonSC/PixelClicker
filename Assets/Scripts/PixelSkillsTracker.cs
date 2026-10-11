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
    private TMP_Text trackerName, trackerXp;
    private Image trackerPanelImage, trackerPinImage;
    private Button trackerClose, trackerSize, trackerStick;
    private Image trackerSizeImage, trackerStickImage;
    private GameObject trackerShackleClosed, trackerShackleOpen;
    private TMP_Text trackerSizeLabel;
    private PixelPopoutDrag trackerDrag;
    private bool trackerOn, trackerSmall, trackerStuck, trackerBuilt;
    private float trackerTimer;

    /// <summary>Is the pinned tracker showing?</summary>
    public bool TrackerOn => trackerOn;

    private void LoadTrackerPrefs()
    {
        trackerOn = PlayerPrefs.GetInt(TrackerOnPref, 0) == 1;
        trackerSmall = PlayerPrefs.GetInt(TrackerSmallPref, 0) == 1;
        trackerStuck = PlayerPrefs.GetInt(TrackerStuckPref, 0) == 1;
    }

    /// <summary>The pin button in the Skills window: close the window and pin the tracker (or, if it is already up, take it away again).</summary>
    private void BuildPinButton()
    {
        Button pin = PixelUIKit.CreateButton(Font, windowObject.transform, "Pin Button", "", new Vector2(70f, 70f), new Color(0.3f, 0.3f, 0.35f, 1f), textColor, 30f);
        RectTransform pr = pin.GetComponent<RectTransform>();
        pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(0f, 1f);
        pr.anchoredPosition = new Vector2(14f, -12f);
        trackerPinImage = pin.GetComponent<Image>();
        // A little pushpin drawn from three rectangles, tilted.
        GameObject icon = new GameObject("Pin Icon", typeof(RectTransform));
        icon.transform.SetParent(pin.transform, false);
        RectTransform ir = icon.GetComponent<RectTransform>();
        ir.anchorMin = ir.anchorMax = ir.pivot = new Vector2(0.5f, 0.5f);
        ir.sizeDelta = new Vector2(40f, 40f);
        ir.localRotation = Quaternion.Euler(0f, 0f, 35f);
        void Part(Vector2 size, Vector2 at, Color c)
        {
            GameObject g = new GameObject("Part", typeof(RectTransform), typeof(Image));
            g.transform.SetParent(icon.transform, false);
            g.GetComponent<Image>().color = c;
            g.GetComponent<Image>().raycastTarget = false;
            RectTransform r = g.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = size;
            r.anchoredPosition = at;
        }
        Part(new Vector2(26f, 8f), new Vector2(0f, 14f), new Color(0.95f, 0.35f, 0.3f));      // the head
        Part(new Vector2(14f, 16f), new Vector2(0f, 4f), new Color(0.85f, 0.25f, 0.22f));    // the body
        Part(new Vector2(20f, 5f), new Vector2(0f, -4f), new Color(0.95f, 0.95f, 1f));       // the collar
        Part(new Vector2(4f, 16f), new Vector2(0f, -14f), new Color(0.85f, 0.88f, 0.95f));   // the needle
        pin.onClick.AddListener(PinTracker);
        PixelHoverTip tip = pin.gameObject.AddComponent<PixelHoverTip>();
        System.Func<string> source = () => "Pin your active skill with an XP bar in a small window you can move";
        tip.onEnter = () => hoverTip = source;
        tip.onExit = () => { if (hoverTip == source) hoverTip = null; };
    }

    private void PinTracker()
    {
        SetTracker(!trackerOn);
        if (trackerOn) PixelPop.Hide(windowObject);   // the Skills window closes and does not come back
    }

    private void SetTracker(bool on)
    {
        trackerOn = on;
        PlayerPrefs.SetInt(TrackerOnPref, on ? 1 : 0);
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
        trackerRect.anchoredPosition = new Vector2(PlayerPrefs.GetFloat(TrackerXPref, -700f), PlayerPrefs.GetFloat(TrackerYPref, 300f));

        trackerDrag = trackerPanel.AddComponent<PixelPopoutDrag>();
        trackerDrag.target = trackerRect;
        trackerDrag.canvas = trackerCanvas.GetComponent<Canvas>();
        trackerDrag.onMoved = () => { PlayerPrefs.SetFloat(TrackerXPref, trackerRect.anchoredPosition.x); PlayerPrefs.SetFloat(TrackerYPref, trackerRect.anchoredPosition.y); };

        trackerName = PixelUIKit.CreateText(Font, trackerPanel.transform, "Name", "", 30f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, textColor);
        trackerName.raycastTarget = false;
        trackerName.enableAutoSizing = true;
        trackerName.fontSizeMin = 12f;

        GameObject track = new GameObject("Bar", typeof(RectTransform), typeof(Image));
        track.transform.SetParent(trackerPanel.transform, false);
        Image trackImage = track.GetComponent<Image>();
        trackImage.color = new Color(0.05f, 0.05f, 0.08f, 1f);
        trackImage.raycastTarget = false;
        PixelUIKit.StyleBox(trackImage, true);
        trackerBar = track.GetComponent<RectTransform>();
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
        trackerClose = PixelUIKit.CreateButton(Font, trackerPanel.transform, "Close", "X", new Vector2(40f, 40f), new Color(0.3f, 0.3f, 0.35f, 1f), textColor, 22f);
        trackerClose.onClick.AddListener(() => SetTracker(false));   // does not bring the Skills window back
        trackerSize = PixelUIKit.CreateButton(Font, trackerPanel.transform, "Size", "-", new Vector2(40f, 40f), new Color(0.3f, 0.3f, 0.35f, 1f), textColor, 26f);
        trackerSizeLabel = trackerSize.GetComponentInChildren<TMP_Text>();
        trackerSizeImage = trackerSize.GetComponent<Image>();
        trackerSize.onClick.AddListener(() =>
        {
            trackerSmall = !trackerSmall;
            PlayerPrefs.SetInt(TrackerSmallPref, trackerSmall ? 1 : 0);
            LayoutTracker();
        });
        trackerStick = PixelUIKit.CreateButton(Font, trackerPanel.transform, "Stick", "", new Vector2(40f, 40f), new Color(0.3f, 0.3f, 0.35f, 1f), textColor, 22f);
        trackerStickImage = trackerStick.GetComponent<Image>();
        trackerShackleClosed = BuildLockPart(trackerStick.transform, false);
        trackerShackleOpen = BuildLockPart(trackerStick.transform, true);
        trackerStick.onClick.AddListener(() =>
        {
            trackerStuck = !trackerStuck;
            PlayerPrefs.SetInt(TrackerStuckPref, trackerStuck ? 1 : 0);
            LayoutTracker();
        });
        foreach (Button b in new[] { trackerClose, trackerSize, trackerStick })
        {
            RectTransform r = b.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(1f, 1f);
        }
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
        bool small = trackerSmall;
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
        float buttonsWidth = -x + 4f;
        RectTransform nr = trackerName.rectTransform;
        nr.anchorMin = new Vector2(0f, 1f);
        nr.anchorMax = new Vector2(1f, 1f);
        nr.pivot = new Vector2(0.5f, 1f);
        nr.offsetMin = new Vector2(16f, small ? -h + 28f : -50f);
        nr.offsetMax = new Vector2(-buttonsWidth - 4f, small ? -4f : -8f);
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
        trackerStickImage.color = trackerStuck ? new Color(0.2f, 0.55f, 0.3f, 1f) : new Color(0.3f, 0.3f, 0.35f, 1f);
        trackerShackleClosed.SetActive(trackerStuck);
        trackerShackleOpen.SetActive(!trackerStuck);
        trackerDrag.locked = trackerStuck;
        KeepTrackerOnScreen();
        RefreshTracker();
    }

    private void KeepTrackerOnScreen()
    {
        if (!trackerBuilt) return;
        RectTransform canvasRect = trackerCanvas.GetComponent<RectTransform>();
        float bars = PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f;
        Vector2 half = canvasRect.rect.size * 0.5f, size = trackerRect.sizeDelta * 0.5f;
        Vector2 p = trackerRect.anchoredPosition;
        p.x = Mathf.Clamp(p.x, -half.x + size.x, half.x - size.x);
        p.y = Mathf.Clamp(p.y, -half.y + bars + size.y, half.y - bars - size.y);
        trackerRect.anchoredPosition = p;
    }

    private void RefreshTracker()
    {
        if (!trackerBuilt) return;
        SkillPack pack = Find(TrainingId);
        if (pack == null) return;
        if (pack.id == StandardId)
        {
            PixelUIKit.SetText(trackerName, "Standard");
            PixelUIKit.SetText(trackerXp, "No skill in training");
            trackerBarFill.anchorMax = new Vector2(0f, 1f);
        }
        else
        {
            int level = LevelOf(pack, out double into, out double need);
            PixelUIKit.SetText(trackerName, pack.displayName + "   Lv " + level);
            PixelUIKit.SetText(trackerXp, PixelClicker.FormatNumberShort(into) + " / " + PixelClicker.FormatNumberShort(need) + " XP");
            trackerBarFill.anchorMax = new Vector2(Mathf.Clamp01((float)(into / System.Math.Max(1d, need))), 1f);
        }
        trackerBarFill.offsetMax = new Vector2(-2f, -2f);
    }

    private void ApplyTrackerVisibility()
    {
        bool show = trackerOn && trackerBuilt && AnyTrainable && !PixelTitleScreen.Showing;
        if (trackerCanvas != null && trackerCanvas.activeSelf != show) trackerCanvas.SetActive(show);
        if (trackerPinImage != null) trackerPinImage.color = trackerOn ? new Color(0.2f, 0.6f, 0.3f, 1f) : new Color(0.3f, 0.3f, 0.35f, 1f);
    }

    private void UpdateTracker()
    {
        if (!built) return;
        if (!trackerBuilt && trackerOn && AnyTrainable) { EnsureTracker(); }
        ApplyTrackerVisibility();
        if (!trackerBuilt || !trackerOn) return;
        trackerTimer -= Time.unscaledDeltaTime;
        if (trackerTimer > 0f) return;
        trackerTimer = 0.25f;
        RefreshTracker();
        KeepTrackerOnScreen();
    }
}
