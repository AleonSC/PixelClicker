// ---- System notes ----
// PixelDidYouKnow.cs
//   "Did you know?" tips (added by PixelClicker.Awake): every few minutes (firstDelaySeconds 180, then a random intervalSeconds 150-300) a random general
//   hint from Assets/Resources/DidYouKnow.txt (one tip per line, '//' lines are notes, {key:Action} placeholders become the player's keys via
//   PixelKeys.Replace, a line starting 'shop:' only shows once the Shop button is available) pops up - only when the game is NOT BUSY (Busy: PixelCalm.ForTip
//   plus no open window, not in first person); a busy moment just waits and retries. Tips come from a shuffled bag, so none repeats before all were seen.
//   Settings > Gameplay 'Did you know tips' (Enabled, PlayerPrefs PixelClicker.Setting.DidYouKnow, per save via PixelSettingsSync).
//   TWO PLACES (PlayerPrefs PixelClicker.Setting.DidYouKnowDock / X / Y, the player's machine, not per save):
//   - DOCKED (default, right edge): a square '?' button docked on the side edge like the other menu buttons (PixelHud.Dock, anchor (1 or 0, 0.5), dockOffsetY
//     300 above the middle; it peeks as a sliver and slides out near the mouse). A tip makes the button slide out (keepOut while showing) and the tip box SLIDE OUT
//     from the edge beside it; a click on the box or showSeconds (12 s, paused while hovered) slides the box away and the button back. Clicking the button shows
//     the last tip again (or hides the one showing).
//   - FLOATING anywhere: the box pops in at its pinned spot and fades out the same way.
//   MOVING it works like the achievement popup: hold the left mouse button on the box (holdSeconds 0.5, PixelHold ring) and it follows the mouse; a left
//   click puts it down - within dockSnap of the left / right screen edge it DOCKS to that edge (the button moves there), anywhere else it floats there; a right
//   click puts it back to the default (docked right, top). A quick click (no hold) just dismisses the tip. Kept inside the black bars.
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
    [Tooltip("How far above the middle of the side edge the docked '?' button sits (canvas units; the tip box slides out beside it).")]
    [SerializeField] private float dockOffsetY = 300f;
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
        get { if (onCache < 0) onCache = PlayerPrefs.GetInt(PrefOn, 1); return onCache != 0; }
        set { onCache = value ? 1 : 0; PlayerPrefs.SetInt(PrefOn, onCache); }
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
    private RectTransform dockButton;   // the '?' button docked on the side edge like the other menu buttons
    private PixelHud hud;
    private bool placing;
    private int placeFrame;
    private bool pressOnBox;
    private float pressTime;

    // ---- UI ----
    private TMP_FontAsset font;
    private RectTransform canvasRect, box, iconRect;
    private Image boxImage;
    private CanvasGroup group;
    private TMP_Text headingLabel, bodyLabel, iconLabel;
    private float boxHeight = 120f;

    private void Start()
    {
        LoadTips();
        nextTipAt = Mathf.Max(5f, firstDelaySeconds);
        dock = Mathf.Clamp(PlayerPrefs.GetInt(PrefDock, 1), 0, 2);
        floatPos = new Vector2(PlayerPrefs.GetFloat(PrefX, 0f), PlayerPrefs.GetFloat(PrefY, 0f));
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
            if (dockButton != null && dockButton.gameObject.activeSelf) dockButton.gameObject.SetActive(false);
            return;
        }
        if (box == null) Build();
        if (box == null) return;
        bool inMenu = PixelCalm.InMenu;
        // The docked '?' button peeks from its edge like the other menu buttons; the box shows only while a tip is out. Both hide on the menus.
        bool visible = !inMenu && (showing || shown > 0.001f || placing);
        if (box.gameObject.activeSelf != visible) box.gameObject.SetActive(visible);
        bool buttonOn = !inMenu && dock != 0;
        if (dockButton != null && dockButton.gameObject.activeSelf != buttonOn) dockButton.gameObject.SetActive(buttonOn);
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
            if (tips.Count > 1 && tip == lastTip) continue;
            return tip;
        }
        return lastTip ?? tips[0];
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
        bool nearRight = local.x > r.width * 0.5f - dockSnap, nearLeft = local.x < -r.width * 0.5f + dockSnap;
        boxImage.color = Color.Lerp(boxColor, new Color(0.35f, 0.3f, 0.1f, 0.98f), (nearLeft || nearRight ? 0.85f : 0.5f) + 0.15f * Mathf.Sin(Time.unscaledTime * 6f));
        group.alpha = 1f;

        bool place = PixelInput.LeftPressed() && Time.frameCount > placeFrame + 1;
        bool reset = PixelInput.RightPressed();
        if (!place && !reset) return;
        if (reset)
        {
            dock = 1;
            PlayerPrefs.DeleteKey(PrefX); PlayerPrefs.DeleteKey(PrefY);
            PlayerPrefs.SetInt(PrefDock, dock);
            DockButton();
        }
        else if (nearLeft || nearRight)
        {
            dock = nearRight ? 1 : 2;
            PlayerPrefs.SetInt(PrefDock, dock);
            DockButton();   // (the '?' button moves to that edge)
        }
        else
        {
            dock = 0;
            floatPos = box.anchoredPosition;
            PlayerPrefs.SetInt(PrefDock, 0);
            PlayerPrefs.SetFloat(PrefX, floatPos.x);
            PlayerPrefs.SetFloat(PrefY, floatPos.y);
        }
        PixelAudio.Play("ui_click");
        placing = false;
        boxImage.color = boxColor;
        // Back to the tip it was showing (it stays a moment where it was put).
        SetTexts(heading, PixelKeys.Replace(lastTip ?? ""));
        showing = !string.IsNullOrEmpty(lastTip);
        showTimer = 3f;
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
        // Docked: the box slides out from the edge to sit beside the '?' button (same height), and back off the screen.
        box.localScale = Vector3.one;
        group.alpha = 1f;
        group.blocksRaycasts = true;
        float buttonW = dockButton != null ? dockButton.sizeDelta.x : 60f;
        float inset = hud != null ? hud.SideShownInset : edgeGap;
        float hy = Mathf.Max(0f, r.height * 0.5f - TopLimit - boxHeight * 0.5f);
        box.anchorMin = box.anchorMax = new Vector2(right ? 1f : 0f, 0.5f);
        box.pivot = new Vector2(right ? 1f : 0f, 0.5f);
        float awayX = boxWidth + 40f, outX = -(inset + buttonW + 12f);
        float x = Mathf.Lerp(awayX, outX, ease);
        box.anchoredPosition = new Vector2(right ? x : -x, Mathf.Clamp(dockOffsetY, -hy, hy));
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

    /// <summary>Docks the '?' button on the box's edge (right or left, dockOffsetY above the middle), square, sliding like the other menu buttons.</summary>
    private void DockButton()
    {
        if (dockButton == null || hud == null) return;
        float square = hud.ButtonSize.x > 0f ? hud.ButtonSize.y / hud.ButtonSize.x : 0.4f;
        hud.Dock(dockButton, new Vector2(dock == 2 ? 0f : 1f, 0.5f), () => showing || placing, dockOffsetY, square);
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

        // The docked '?' button: peeks from the side edge like the other menu buttons, slides out while a tip shows; a click shows the last tip again.
        Button b2 = PixelUIKit.CreateButton(font, canvasObject.transform, "Did You Know Button", "?", new Vector2(60f, 60f), iconBoxColor, accentColor, 34f);
        dockButton = b2.GetComponent<RectTransform>();
        b2.onClick.AddListener(() =>
        {
            if (showing) Hide();
            else if (!string.IsNullOrEmpty(lastTip)) ShowTip(lastTip);
            else if (tips.Count > 0) ShowTip(NextTip());
        });
        hud = PixelHud.Ensure(gameObject);
        DockButton();

        SetTexts(heading, "");
        shown = 0f;
        Layout();
    }
}
