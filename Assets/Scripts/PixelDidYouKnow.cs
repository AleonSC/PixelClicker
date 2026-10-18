// ---- System notes ----
// PixelDidYouKnow.cs
//   "Did you know?" tips (added by PixelClicker.Awake): every few minutes (firstDelaySeconds 180, then a random intervalSeconds 150-300) a random general
//   hint from Assets/Resources/DidYouKnow.txt (one tip per line, '//' lines are notes, {key:Action} placeholders become the player's keys via
//   PixelKeys.Replace, a line starting 'shop:' only shows once the Shop button is available) pops up - only when the game is NOT BUSY (Busy: PixelCalm.ForTip
//   plus no open window, not in first person); a busy moment just waits and retries. Tips come from a shuffled bag, so none repeats before all were seen.
//   Settings > Gameplay 'Did you know tips' (Enabled, PlayerPrefs PixelClicker.Setting.DidYouKnow, per save via PixelSettingsSync).
//   TWO PLACES (PlayerPrefs PixelClicker.Setting.DidYouKnowDock / X / Y, the player's machine, not per save):
//   - DOCKED (default: right edge, near the top) at the height where it was dropped: the box hides just past the side edge (dockY = its centre, canvas
//     units from the middle, saved). When it is docked it slides into the side; when a tip triggers it SLIDES OUT, stays showSeconds (12 s, paused while
//     hovered; a click sends it back at once) and slides back in. No button is left on screen.
//   - FLOATING anywhere: the box pops in at its pinned spot and fades out the same way.
//   MOVING it works like the achievement popup: hold the left mouse button on the box (holdSeconds 0.5, PixelHold ring) and it follows the mouse; a left
//   click puts it down - within dockSnap of the left / right screen edge it DOCKS to that edge at that height, anywhere else it floats there; a right
//   click puts it back to the default (docked right, top), as does Settings > Accessibility 'Reset tips location' (ResetLocation). A quick click (no hold) just
//   dismisses the tip. Kept inside the black bars.
//   DOCK FEEDBACK (2026-10-16): while moving, both side edges show a glowing dock zone (the one in range glows brightly and the heading reads 'Click to dock
//   it on the right edge'); in range = the mouse OR the box's edge is within dockSnap (the box is clamped inside the screen, so the mouse test alone could miss).
//   Right after docking the box says where it went (dockedText, dockedShowSeconds 2.5) before sliding in, and while tucked away a small gold mark sits on
//   that edge at its height (showDockMark).
// ---------------------------------------------------
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Random "Did you know?" hints in a small box that slides out from a screen edge (or pops in where the player pinned it) when the game is calm.</summary>
public class PixelDidYouKnow : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip("Seconds of play before the first tip can show.")]
    [SerializeField] private float firstDelaySeconds = 180f;
    [Tooltip("Random seconds between two tips (min, max).")]
    [SerializeField] private Vector2 intervalSeconds = new Vector2(150f, 300f);
    [Tooltip("How long a tip stays before it slides / fades away by itself (the timer waits while the mouse is on it).")]
    [SerializeField] private float showSeconds = 12f;
    [Tooltip("Seconds the box takes to slide out / away (or pop in / out when floating).")]
    [SerializeField] private float slideSeconds = 0.35f;
    [Tooltip("How long a busy moment must have been calm before a due tip shows (seconds).")]
    [SerializeField] private float calmSeconds = 2f;

    [Header("Text")]
    [Tooltip("Resources text file with the tips (one per line; '//' lines are notes; 'shop:' = only once the Shop button is available).")]
    [SerializeField] private string tipsResource = "DidYouKnow";
    [Tooltip("The heading above every tip.")]
    [SerializeField] private string heading = "Did you know?";
    [Tooltip("The heading while the box is being moved.")]
    [SerializeField] private string placeHeading = "Click to put it here";
    [Tooltip("The hint under the heading while the box is being moved.")]
    [SerializeField] private string placeText = "Put it at the left or right screen edge to dock it there. Right-click puts it back.";
    [Tooltip("Heading while the box is close enough to a side edge to dock there ({side} = left / right).")]
    [SerializeField] private string dockHereHeading = "Click to dock it on the {side} edge";
    [Tooltip("Text shown for a moment right after docking ({side} = left / right).")]
    [SerializeField] private string dockedText = "Docked on the {side} edge. New tips slide out from here; the small gold mark shows where it is.";
    [Tooltip("Font size of the heading.")]
    [SerializeField] private float headingFontSize = 24f;
    [Tooltip("Font size of the tip.")]
    [SerializeField] private float bodyFontSize = 21f;

    [Header("Box")]
    [Tooltip("Width of the box (canvas units).")]
    [SerializeField] private float boxWidth = 500f;
    [Tooltip("Size of the '?' icon square on the box's inner side.")]
    [SerializeField] private float iconSize = 52f;
    [Tooltip("Gap from the screen edge / black bar when shown (canvas units).")]
    [SerializeField] private float edgeGap = 16f;
    [Tooltip("Seconds a freshly docked box stays out before it slides into the side (so you see where it went).")]
    [SerializeField] private float dockedShowSeconds = 2.5f;
    [Tooltip("Width of the glowing dock zones shown at both side edges while the box is being moved (canvas units).")]
    [SerializeField] private float dockZoneWidth = 26f;
    [Tooltip("Show a small gold mark at the docked edge while the box is tucked away, so you can see where tips will come from.")]
    [SerializeField] private bool showDockMark = true;
    [Tooltip("Putting the box down this close to the left / right screen edge docks it there (canvas units).")]
    [SerializeField] private float dockSnap = 120f;
    [Tooltip("Hold the left mouse button this long on the box to pick it up (seconds).")]
    [SerializeField] private float holdSeconds = 0.5f;
    [Tooltip("Sorting order of the box's canvas (above windows, below tip boxes / notices).")]
    [SerializeField] private int sortingOrder = 560;
    [Tooltip("Box colour.")]
    [SerializeField] private Color boxColor = new Color(0.08f, 0.09f, 0.13f, 0.96f);
    [Tooltip("Colour of the icon square.")]
    [SerializeField] private Color iconBoxColor = new Color(0.16f, 0.17f, 0.24f, 1f);
    [Tooltip("Colour of the '?' and the heading.")]
    [SerializeField] private Color accentColor = new Color(1f, 0.82f, 0.3f, 1f);
    [Tooltip("Colour of the tip text.")]
    [SerializeField] private Color bodyColor = new Color(0.92f, 0.93f, 0.97f, 1f);
    [Tooltip("Colour of the ring that fills while holding the box.")]
    [SerializeField] private Color ringColor = new Color(1f, 0.82f, 0.3f, 1f);

    private const string PrefOn = "PixelClicker.Setting.DidYouKnow";
    private const string PrefDock = "PixelClicker.Setting.DidYouKnowDock";   // 0 floating, 1 right edge, 2 left edge
    private const string PrefX = "PixelClicker.Setting.DidYouKnowX";
    private const string PrefY = "PixelClicker.Setting.DidYouKnowY";

    private static int onCache = -1;

    /// <summary>Settings > Gameplay 'Did you know tips' (on by default).</summary>
    public static bool Enabled
    {
        get { if (onCache < 0) onCache = PixelPrefs.GetInt(PrefOn, 1); return onCache != 0; }
        set { onCache = value ? 1 : 0; PixelPrefs.SetInt(PrefOn, onCache); }
    }

    /// <summary>Forgets the cached setting (a save's settings were loaded).</summary>
    public static void ReloadFromPrefs() { onCache = -1; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { onCache = -1; }

    // ---- state ----
    private readonly List<string> tips = new List<string>();
    private readonly List<int> bag = new List<int>();
    private string lastTip;
    private float playTime, nextTipAt, calmTimer;
    private bool showing;          // a tip is out (or sliding out)
    private float showTimer;
    private float shown;           // 0 = away .. 1 = fully out
    private int dock = 1;          // 0 floating, 1 right, 2 left
    private Vector2 floatPos;      // floating: the box's centre, canvas units from the screen's middle
    private float dockY = float.NaN;    // docked: the box's centre height, canvas units from the middle (NaN = the default spot near the top)
    private bool placing;
    private int placeFrame;
    private bool pressOnBox;
    private float pressTime;

    // ---- UI ----
    private TMP_FontAsset font;
    private RectTransform canvasRect, box, iconRect;
    private Image boxImage, zoneLeft, zoneRight, dockMark;
    private CanvasGroup group;
    private TMP_Text headingLabel, bodyLabel, iconLabel;
    private float boxHeight = 120f;

    private void Start()
    {
        LoadTips();
        nextTipAt = Mathf.Max(5f, firstDelaySeconds);
        dock = Mathf.Clamp(PixelPrefs.GetInt(PrefDock, 1), 0, 2);
        floatPos = new Vector2(PixelPrefs.GetFloat(PrefX, 0f), PixelPrefs.GetFloat(PrefY, 0f));
        dockY = dock != 0 && PixelPrefs.HasKey(PrefY) ? PixelPrefs.GetFloat(PrefY, 0f) : float.NaN;
    }

    private void LoadTips()
    {
        tips.Clear();
        TextAsset file = Resources.Load<TextAsset>(tipsResource);
        if (file == null) return;
        foreach (string raw in file.text.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("//")) continue;
            tips.Add(line);
        }
    }

    /// <summary>The game has something going on: no tip now.</summary>
    private static bool Busy => !PixelCalm.ForTip || PixelWindows.AnyOpen() || PixelFirstPerson.Active;

    private void Update()
    {
        if (!Enabled)
        {
            if (box != null && box.gameObject.activeSelf) { showing = false; placing = false; shown = 0f; box.gameObject.SetActive(false); }
            if (box != null) UpdateMarks(true);   // (hides the dock zones / mark)
            return;
        }
        if (box == null) Build();
        if (box == null) return;
        bool inMenu = PixelCalm.InMenu;
        // The box shows only while a tip is out (docked: sliding out of / back into its side); it hides on the menus.
        bool visible = !inMenu && (showing || shown > 0.001f || placing);
        if (box.gameObject.activeSelf != visible) box.gameObject.SetActive(visible);
        UpdateMarks(inMenu);
        if (inMenu) return;

        float dt = Time.unscaledDeltaTime;
        if (!showing && !placing)
        {
            playTime += Time.timeScale > 0f ? dt : 0f;
            calmTimer = Busy ? 0f : calmTimer + dt;
            if (playTime >= nextTipAt && calmTimer >= calmSeconds && tips.Count > 0) ShowTip(NextTip());
        }

        if (placing) { TickPlacing(); return; }
        HandlePointer();

        if (showing)
        {
            bool hovered = Over();
            if (!hovered) showTimer -= dt;
            if (showTimer <= 0f || (!hovered && PixelMinigame.TakeoverActive)) Hide();   // (a takeover minigame starting sends it away)
        }
        float target = showing ? 1f : 0f;
        shown = Mathf.MoveTowards(shown, target, dt / Mathf.Max(0.05f, slideSeconds));
        Layout();
    }

    private string NextTip()
    {
        PixelShop shop = PixelFind.First<PixelShop>();
        bool shopOn = shop != null && shop.ShopAvailable;
        for (int tries = 0; tries < tips.Count * 2; tries++)
        {
            if (bag.Count == 0) { for (int i = 0; i < tips.Count; i++) bag.Add(i); }
            int pick = Random.Range(0, bag.Count);
            string tip = tips[bag[pick]];
            bag.RemoveAt(pick);
            if (tip.StartsWith("shop:")) { if (!shopOn) continue; tip = tip.Substring(5).Trim(); }
            if (PixelSkills.StandardHidden && tip.Contains("Standard")) continue;   // (the mystery bar: no tip names Standard before its level 2 reveal)
            if (tips.Count > 1 && tip == lastTip) continue;
            return tip;
        }
        return lastTip ?? tips[0];
    }

    /// <summary>Dev Events tab: shows the next tip right away (even when the game isn't calm). False when there are no tips.</summary>
    public bool DevShowNow()
    {
        if (tips.Count == 0) return false;
        ShowTip(NextTip());
        return true;
    }

    private void ShowTip(string tip)
    {
        lastTip = tip;
        SetTexts(heading, PixelKeys.Replace(tip));
        showing = true;
        showTimer = Mathf.Max(2f, showSeconds);
        nextTipAt = playTime + Random.Range(Mathf.Min(intervalSeconds.x, intervalSeconds.y), Mathf.Max(intervalSeconds.x, intervalSeconds.y)) + showTimer;
        PixelAudio.Play("ui_click");
    }

    private void Hide() { showing = false; }

    // ------------------------------------------------------------------
    // Pointer: quick click on the box = dismiss; hold = pick it up (the docked '?' button shows the last tip again)
    // ------------------------------------------------------------------

    private bool Over() => box != null && box.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(box, PixelInput.PointerPosition(), null);

    private void HandlePointer()
    {
        bool over = Over();
        bool canHold = over && showing;
        if (PixelInput.LeftPressed() && over) { pressOnBox = true; pressTime = Time.unscaledTime; }
        if (canHold && PixelHold.Update(this, box, Mathf.Max(0.1f, holdSeconds), "", ringColor, true, null, true))
        {
            pressOnBox = false;
            placing = true;
            placeFrame = Time.frameCount;
            shown = 1f;
            group.alpha = 1f;
            SetTexts(placeHeading, placeText);
            return;
        }
        if (!canHold) PixelHold.Update(this, null, 1f, "", ringColor, true, null, true);
        if (pressOnBox && !PixelInput.LeftHeld())
        {
            pressOnBox = false;
            if (over && Time.unscaledTime - pressTime < 0.3f)
            {
                if (showing) Hide();
                PixelAudio.Play("ui_click");
            }
        }
    }

    /// <summary>The box follows the mouse; a left click puts it down (docking at a side edge), a right click puts it back to the default spot.</summary>
    private void TickPlacing()
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, PixelInput.PointerPosition(), null, out Vector2 local)) return;
        Rect r = canvasRect.rect;
        // Centred on the mouse, kept inside the bars.
        box.anchorMin = box.anchorMax = new Vector2(0.5f, 0.5f);
        box.pivot = new Vector2(0.5f, 0.5f);
        box.anchoredPosition = ClampFloat(local);
        // In range of a side: the mouse OR the box's own edge is near it (the box is kept inside the screen, so its edge is the surer test).
        float boxRight = box.anchoredPosition.x + boxWidth * 0.5f, boxLeft = box.anchoredPosition.x - boxWidth * 0.5f;
        bool nearRight = local.x > r.width * 0.5f - dockSnap || boxRight > r.width * 0.5f - dockSnap * 0.5f;
        bool nearLeft = !nearRight && (local.x < -r.width * 0.5f + dockSnap || boxLeft < -r.width * 0.5f + dockSnap * 0.5f);
        placeNear = nearRight ? 1 : nearLeft ? 2 : 0;
        headingLabel.text = placeNear != 0 ? dockHereHeading.Replace("{side}", SideName(placeNear)) : placeHeading;
        boxImage.color = Color.Lerp(boxColor, new Color(0.35f, 0.3f, 0.1f, 0.98f), (nearLeft || nearRight ? 0.85f : 0.5f) + 0.15f * Mathf.Sin(Time.unscaledTime * 6f));
        group.alpha = 1f;

        bool place = PixelInput.LeftPressed() && Time.frameCount > placeFrame + 1;
        bool reset = PixelInput.RightPressed();
        if (!place && !reset) return;
        bool docked = false;
        if (reset) { ResetLocation(); docked = true; }
        else if (nearLeft || nearRight)
        {
            dock = nearRight ? 1 : 2;
            dockY = box.anchoredPosition.y;   // (docks at the height where it was dropped)
            PixelPrefs.SetInt(PrefDock, dock);
            PixelPrefs.SetFloat(PrefY, dockY);
            docked = true;
        }
        else
        {
            dock = 0;
            floatPos = box.anchoredPosition;
            PixelPrefs.SetInt(PrefDock, 0);
            PixelPrefs.SetFloat(PrefX, floatPos.x);
            PixelPrefs.SetFloat(PrefY, floatPos.y);
        }
        PixelAudio.Play("ui_click");
        placing = false;
        boxImage.color = boxColor;
        placeNear = 0;
        // Back to the tip it was showing. Floating: it stays a moment where it was put. Docked: it says where it went, then slides into the side.
        if (docked) SetTexts(heading, dockedText.Replace("{side}", SideName(dock)));
        else SetTexts(heading, PixelKeys.Replace(lastTip ?? ""));
        showing = docked || !string.IsNullOrEmpty(lastTip);
        showTimer = docked ? Mathf.Max(0.1f, dockedShowSeconds) : 3f;
        shown = 1f;
        Layout();
    }

    // ------------------------------------------------------------------
    // Layout
    // ------------------------------------------------------------------

    private float TopLimit => PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f;

    private Vector2 ClampFloat(Vector2 centre)
    {
        Rect r = canvasRect.rect;
        float bar = TopLimit;
        float hx = Mathf.Max(0f, r.width * 0.5f - boxWidth * 0.5f);
        float hy = Mathf.Max(0f, r.height * 0.5f - bar - boxHeight * 0.5f);
        return new Vector2(Mathf.Clamp(centre.x, -hx, hx), Mathf.Clamp(centre.y, -hy, hy));
    }

    private void Layout()
    {
        if (box == null) return;
        Rect r = canvasRect.rect;
        bool right = dock != 2;
        // The '?' square sits on the box's inner side when docked (so it is what peeks out), on the left when floating.
        bool iconLeft = dock != 2;
        iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(iconLeft ? 0f : 1f, 0.5f);
        iconRect.anchoredPosition = new Vector2(iconLeft ? 14f : -14f, 0f);
        float textInset = 14f + iconSize + 14f;
        RectTransform tr = (RectTransform)bodyLabel.transform.parent;
        tr.offsetMin = new Vector2(iconLeft ? textInset : 16f, 12f);
        tr.offsetMax = new Vector2(iconLeft ? -16f : -textInset, -10f);

        float ease = shown * shown * (3f - 2f * shown);   // smoothstep
        if (dock == 0)
        {
            box.anchorMin = box.anchorMax = new Vector2(0.5f, 0.5f);
            box.pivot = new Vector2(0.5f, 0.5f);
            box.anchoredPosition = ClampFloat(floatPos);
            box.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, ease);
            group.alpha = ease;
            group.blocksRaycasts = shown > 0.5f;
            return;
        }
        // Docked: the box slides out of its side edge (at its own height) and back in past the edge.
        box.localScale = Vector3.one;
        group.alpha = 1f;
        group.blocksRaycasts = shown > 0.3f;
        float hy = Mathf.Max(0f, r.height * 0.5f - TopLimit - boxHeight * 0.5f);
        float y = float.IsNaN(dockY) ? hy - edgeGap : dockY;   // (default: near the top, just under the bar)
        box.anchorMin = box.anchorMax = new Vector2(right ? 1f : 0f, 0.5f);
        box.pivot = new Vector2(right ? 1f : 0f, 0.5f);
        float awayX = boxWidth + 40f, outX = -edgeGap;
        float x = Mathf.Lerp(awayX, outX, ease);
        box.anchoredPosition = new Vector2(right ? x : -x, Mathf.Clamp(y, -hy, hy));
    }

    private int placeNear;   // while moving: 0 = no side in range, 1 = right, 2 = left

    private static string SideName(int side) => side == 2 ? "left" : "right";

    /// <summary>The dock zones while moving the box (both edges glow, the one in range brightly) and the small gold mark at the docked edge while the box is tucked away.</summary>
    private void UpdateMarks(bool inMenu)
    {
        if (zoneLeft == null) return;
        Rect r = canvasRect.rect;
        float bar = TopLimit;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
        bool zones = placing && !inMenu;
        for (int i = 0; i < 2; i++)
        {
            Image z = i == 0 ? zoneRight : zoneLeft;
            if (z.gameObject.activeSelf != zones) z.gameObject.SetActive(zones);
            if (!zones) continue;
            bool hot = placeNear == (i == 0 ? 1 : 2);
            RectTransform zr = z.rectTransform;
            zr.sizeDelta = new Vector2(dockZoneWidth * (hot ? 1.6f : 1f), Mathf.Max(0f, r.height - bar * 2f));
            Color c = accentColor; c.a = hot ? 0.55f + 0.35f * pulse : 0.18f + 0.1f * pulse;
            z.color = c;
        }
        bool mark = showDockMark && !inMenu && !placing && dock != 0 && shown < 0.05f;
        if (dockMark.gameObject.activeSelf != mark) dockMark.gameObject.SetActive(mark);
        if (!mark) return;
        bool right = dock != 2;
        float hy = Mathf.Max(0f, r.height * 0.5f - bar - boxHeight * 0.5f);
        float y = float.IsNaN(dockY) ? hy - edgeGap : dockY;
        RectTransform mr = dockMark.rectTransform;
        mr.anchorMin = mr.anchorMax = mr.pivot = new Vector2(right ? 1f : 0f, 0.5f);
        mr.anchoredPosition = new Vector2(0f, Mathf.Clamp(y, -hy, hy));
        mr.sizeDelta = new Vector2(7f, Mathf.Clamp(boxHeight * 0.6f, 40f, 90f));
        Color mc = accentColor; mc.a = 0.55f;
        dockMark.color = mc;
    }

    private Image MakeEdgeImage(string name, bool right)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(canvasRect, false);
        go.transform.SetAsFirstSibling();   // (under the box)
        Image img = go.GetComponent<Image>();
        img.raycastTarget = false;
        PixelUIKit.StyleBox(img, true);
        RectTransform rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(right ? 1f : 0f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        go.SetActive(false);
        return img;
    }

    /// <summary>Settings > 'Reset tips location' (and a right click while moving it): docked at the right edge near the top again.</summary>
    public void ResetLocation()
    {
        dock = 1;
        dockY = float.NaN;
        PixelPrefs.DeleteKey(PrefX);
        PixelPrefs.DeleteKey(PrefY);
        PixelPrefs.SetInt(PrefDock, dock);
        if (box != null) Layout();
    }

    private void SetTexts(string head, string body)
    {
        headingLabel.text = head;
        bodyLabel.text = body;
        float textW = boxWidth - (14f + iconSize + 14f) - 16f;
        float bodyH = bodyLabel.GetPreferredValues(body, textW, 0f).y;
        boxHeight = Mathf.Max(iconSize + 28f, 12f + headingFontSize * 1.35f + bodyH + 18f);
        box.sizeDelta = new Vector2(boxWidth, boxHeight);
    }

    private void Build()
    {
        PixelClicker clicker = PixelFind.First<PixelClicker>();
        font = clicker != null ? clicker.UIFont : null;
        GameObject canvasObject = PixelUIKit.CreateCanvas("Pixel Did You Know", sortingOrder, new Vector2(1920f, 1080f), true);
        canvasObject.transform.SetParent(transform, false);
        canvasRect = canvasObject.GetComponent<RectTransform>();
        PixelUIKit.EnsureEventSystem();

        GameObject panel = new GameObject("Tip Box", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        panel.transform.SetParent(canvasObject.transform, false);
        box = panel.GetComponent<RectTransform>();
        boxImage = panel.GetComponent<Image>();
        boxImage.color = boxColor;
        boxImage.raycastTarget = true;   // (clicks on it never reach the cube)
        PixelUIKit.StyleBox(boxImage);
        group = panel.GetComponent<CanvasGroup>();

        GameObject icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        icon.transform.SetParent(panel.transform, false);
        Image iconImage = icon.GetComponent<Image>();
        iconImage.color = iconBoxColor;
        iconImage.raycastTarget = false;
        PixelUIKit.StyleBox(iconImage, true);
        iconRect = icon.GetComponent<RectTransform>();
        iconRect.sizeDelta = new Vector2(iconSize, iconSize);
        iconLabel = PixelUIKit.CreateText(font, icon.transform, "Mark", "?", iconSize * 0.72f, TextAlignmentOptions.Center, FontStyles.Bold, accentColor);
        PixelUIKit.Stretch(iconLabel.rectTransform);

        GameObject texts = new GameObject("Texts", typeof(RectTransform));
        texts.transform.SetParent(panel.transform, false);
        RectTransform tr = texts.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
        headingLabel = PixelUIKit.CreateText(font, texts.transform, "Heading", heading, headingFontSize, TextAlignmentOptions.TopLeft, FontStyles.Bold, accentColor);
        RectTransform hr = headingLabel.rectTransform;
        hr.anchorMin = new Vector2(0f, 1f); hr.anchorMax = new Vector2(1f, 1f); hr.pivot = new Vector2(0.5f, 1f);
        hr.sizeDelta = new Vector2(0f, headingFontSize * 1.35f);
        hr.anchoredPosition = Vector2.zero;
        bodyLabel = PixelUIKit.CreateText(font, texts.transform, "Tip", "", bodyFontSize, TextAlignmentOptions.TopLeft, FontStyles.Normal, bodyColor);
        RectTransform b = bodyLabel.rectTransform;
        b.anchorMin = Vector2.zero; b.anchorMax = Vector2.one;
        b.offsetMin = Vector2.zero; b.offsetMax = new Vector2(0f, -headingFontSize * 1.35f);
        bodyLabel.textWrappingMode = TextWrappingModes.Normal;

        zoneRight = MakeEdgeImage("Dock Zone Right", true);
        zoneLeft = MakeEdgeImage("Dock Zone Left", false);
        dockMark = MakeEdgeImage("Dock Mark", dock != 2);

        SetTexts(heading, "");
        shown = 0f;
        Layout();
    }
}
