using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A small "tip" box for the player: <c>PixelNotice.Show("text")</c> puts a box with a message and a Got it button at the
/// top of the screen, just under the black bar. Used for one-off hints (the first black hole, for example). Only one box
/// shows at a time; a new message replaces the old one. Closes on its button, or after the given time.
/// </summary>
public static class PixelNotice
{
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() // keeps static state clean when Enter Play Mode skips the domain reload
    {
        box = null;
        FixedSize = Vector2.zero;
        SideAnchor = null;
        SideWidth = 0f;
        SideGap = 0f;
    }

    private static PixelNoticeBox box;

    /// <summary>Shows a message. 'seconds' = 0 keeps it until the player closes it.</summary>
    /// <summary>'compact' = the small text-sized box at the top of the screen (tutorial tips next to the buttons), even if a fixed size is set.</summary>
    /// <summary>'small' = a much smaller box (narrow, small text and button) at the top of the screen: for quick "this just happened" tips.</summary>
    /// <summary>'nearLog' = a small box at the bottom left, next to the event log.</summary>
    /// <summary>'aboveCombo' = a small box at the bottom of the screen, just above the combo meter.</summary>
    /// <summary>'nearScreen' = the box follows a point on screen (pixels; null = nowhere), sitting beside it, e.g. next to the ghost. 'freezeTime' = Time.timeScale is 0 until the box is closed.</summary>
    /// <summary>'onBack' = a small back arrow appears at the box's top left; holding it closes this box (without running onClosed) and calls onBack (tutorial steps go to the previous step).</summary>
    /// <summary>'onSkip' = a small Skip button at the top right; holding it closes this box (without running onClosed) and calls onSkip (a tutorial ends the whole tutorial).</summary>
    /// <summary>'beside' = a window (Inventory, Log): the box sits right next to it, to its right, never overlapping it. 'besideBottom' aligns the bottoms (for a window at the bottom of the screen), else the tops.</summary>
    public static void Show(string message, float seconds = 0f, System.Action onClosed = null, bool compact = false, bool small = false, bool aboveCombo = false, bool nearLog = false,
                            RectTransform beside = null, bool besideBottom = false, System.Func<Vector2?> nearScreen = null, bool freezeTime = false, float minHeight = 0f, System.Action onBack = null, System.Action onSkip = null)
    {
        if (box == null) box = new GameObject("Pixel Notice").AddComponent<PixelNoticeBox>();
        box.Open(PixelKeys.Replace(message), seconds, onClosed, compact, small || aboveCombo || nearLog, aboveCombo, nearLog, beside, besideBottom, nearScreen, freezeTime, minHeight, onBack, onSkip); // {key:...} placeholders show the player's keys
    }

    /// <summary>A fixed box size (canvas units) for every tip box, shown centred on screen. Vector2.zero = size to the text, at the top.</summary>
    public static Vector2 FixedSize { get; set; }

    /// <summary>
    /// With FixedSize set and the shop open, tip boxes sit to the RIGHT of this window (the shop panel): FixedSize.y tall (the shop's
    /// height), SideWidth wide, SideGap away from it - the mirror of the shop's currency panel on the left.
    /// </summary>
    public static RectTransform SideAnchor { get; set; }
    public static float SideWidth { get; set; }
    public static float SideGap { get; set; }

    /// <summary>Is a tip box on screen right now?</summary>
    public static bool IsShowing => box != null && box.IsOpen;

    /// <summary>Closes the box if it is showing.</summary>
    public static void Hide()
    {
        if (box != null) box.Close();
    }
}

/// <summary>The box itself (built the first time it is needed).</summary>
public class PixelNoticeBox : MonoBehaviour
{
    private const float Width = 900f;
    private const float SmallWidth = 470f;

    private GameObject canvasRoot;
    private TMP_Text label;
    private RectTransform boxRect, okRect;
    private TMP_Text okLabel;
    private float timer;
    private bool timed;

    private void Build()
    {
        PixelClicker clicker = PixelFind.First<PixelClicker>();
        TMP_FontAsset font = clicker != null ? clicker.UIFont : null;
        PixelUIKit.EnsureEventSystem();

        canvasRoot = PixelUIKit.CreateCanvas("Pixel Notice Canvas", 620, new Vector2(1920f, 1080f), true);
        canvasRoot.transform.SetParent(transform, false);

        GameObject box = new GameObject("Box", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(canvasRoot.transform, false);
        PixelUIKit.StyleWindow(box.GetComponent<Image>(), new Color(0.1f, 0.22f, 0.32f, 0.97f));
        box.AddComponent<PixelPop>();   // grows when shown, shrinks when closed
        boxRect = box.GetComponent<RectTransform>();
        boxRect.anchorMin = boxRect.anchorMax = boxRect.pivot = new Vector2(0.5f, 1f);

        label = PixelUIKit.CreateText(font, box.transform, "Text", "", 30f, TextAlignmentOptions.Center, FontStyles.Normal, Color.white);
        RectTransform lr = label.rectTransform;
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = Vector2.one;
        lr.offsetMin = new Vector2(30f, 90f);
        lr.offsetMax = new Vector2(-30f, -20f);

        Button ok = PixelUIKit.CreateButton(font, box.transform, "Ok", "Got it", new Vector2(200f, 60f),
                                            new Color(0.25f, 0.5f, 0.7f, 1f), Color.white, 30f);
        RectTransform okr = ok.GetComponent<RectTransform>();
        okr.anchorMin = okr.anchorMax = okr.pivot = new Vector2(0.5f, 0f);
        okr.anchoredPosition = new Vector2(0f, 18f);
        ok.onClick.AddListener(OnOk);
        okRect = okr;
        okLabel = ok.GetComponentInChildren<TMP_Text>();

        // A small back arrow at the top left: hold it to go back (only shown when the caller gave an onBack).
        Button back = PixelUIKit.CreateButton(font, box.transform, "Back", "<", new Vector2(46f, 40f), new Color(0.2f, 0.35f, 0.5f, 1f), Color.white, 26f);
        PixelUIKit.UseArrowGlyph(back, true);
        backRect = back.GetComponent<RectTransform>();
        backRect.anchorMin = backRect.anchorMax = backRect.pivot = new Vector2(0f, 1f);
        backRect.anchoredPosition = new Vector2(10f, -10f);
        muteRect = PixelTalk.AddMuteButton(font, box.GetComponent<RectTransform>(), new Vector2(40f, 40f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(62f, -10f), new Color(0.2f, 0.35f, 0.5f, 1f)).GetComponent<RectTransform>();   // mutes the talking blips
        GameObject fillGo = new GameObject("Hold Fill", typeof(RectTransform), typeof(Image));
        fillGo.transform.SetParent(back.transform, false);
        Image fillImage = fillGo.GetComponent<Image>();
        fillImage.color = new Color(1f, 1f, 1f, 0.45f);
        fillImage.raycastTarget = false;
        RectTransform fillRect = fillGo.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
        fillRect.SetSiblingIndex(1);
        BuildSkip(box.transform, font);
        PixelHoldButton hold = back.gameObject.AddComponent<PixelHoldButton>();
        hold.Setup(fillRect, backHoldSeconds, () =>
        {
            hold.ResetProgress();
            System.Action go = backCallback;
            closedCallback = null;   // going back is not 'Got it'
            Close();
            go?.Invoke();
        });
    }

    private RectTransform backRect, muteRect, skipRect;
    private System.Action skipCallback;
    private const float skipHoldSeconds = 0.9f;

    /// <summary>The Skip button (top right, hold to use): ends the whole tutorial this box belongs to.</summary>
    private void BuildSkip(Transform parent, TMPro.TMP_FontAsset font)
    {
        Button skip = PixelUIKit.CreateButton(font, parent, "Skip", "Skip", new Vector2(86f, 40f), new Color(0.35f, 0.25f, 0.3f, 1f), Color.white, 22f);
        skipRect = skip.GetComponent<RectTransform>();
        skipRect.anchorMin = skipRect.anchorMax = skipRect.pivot = new Vector2(1f, 1f);
        skipRect.anchoredPosition = new Vector2(-10f, -10f);
        GameObject fillGo = new GameObject("Hold Fill", typeof(RectTransform), typeof(Image));
        fillGo.transform.SetParent(skip.transform, false);
        Image fillImage = fillGo.GetComponent<Image>();
        fillImage.color = new Color(1f, 1f, 1f, 0.45f);
        fillImage.raycastTarget = false;
        RectTransform fillRect = fillGo.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
        fillRect.SetSiblingIndex(1);
        PixelHoldButton hold = skip.gameObject.AddComponent<PixelHoldButton>();
        hold.Setup(fillRect, skipHoldSeconds, () =>
        {
            hold.ResetProgress();
            System.Action go = skipCallback;
            closedCallback = null;   // skipping is not 'Got it'
            Close();
            go?.Invoke();
        });
    }
    private System.Action backCallback;
    private const float backHoldSeconds = 0.6f;

    // The text types itself out; the first Got it press shows the rest at once, the next one closes the box.
    private bool typing;
    private float typeShown;
    private int typeTotal;
    private const float TypeCharsPerSecond = 80f;

    private void OnOk()
    {
        if (typing) { FinishTyping(); return; }
        Close();
    }

    private void FinishTyping()
    {
        typing = false;
        if (label != null) label.maxVisibleCharacters = int.MaxValue;
    }

    private const float BesideWidth = 640f;

    public void Open(string message, float seconds, System.Action onClosed = null, bool compact = false, bool small = false, bool aboveCombo = false, bool nearLog = false,
                     RectTransform beside = null, bool besideBottom = false, System.Func<Vector2?> nearScreen = null, bool freezeTime = false, float minHeight = 0f, System.Action onBack = null, System.Action onSkip = null)
    {
        closedCallback = onClosed;
        skipCallback = onSkip;
        backCallback = onBack; // a tip replaced by a new one never runs the old callback
        besideTarget = null;
        Unfreeze();
        followPoint = nearScreen;
        if (canvasRoot == null) Build();

        // The small box: narrow, small text, small button.
        bool besideWindow = beside != null && beside.gameObject.activeInHierarchy;
        float width = besideWindow ? (small ? 440f : BesideWidth) : small ? SmallWidth : Width;
        float textSize = small ? 22f : 30f;
        label.fontSize = textSize;
        label.rectTransform.offsetMin = small ? new Vector2(18f, 62f) : new Vector2(30f, 90f);
        label.rectTransform.offsetMax = small ? new Vector2(-18f, onSkip != null ? -52f : -12f) : new Vector2(-30f, -20f);   // (a Skip button sits at the top right)
        okRect.sizeDelta = small ? new Vector2(150f, 44f) : new Vector2(200f, 60f);
        okRect.anchoredPosition = new Vector2(0f, small ? 10f : 18f);
        if (okLabel != null) okLabel.fontSize = small ? 22f : 30f;

        label.text = message;
        Vector2 fixedSize = compact || small || besideWindow ? Vector2.zero : PixelNotice.FixedSize;
        if (besideWindow)
        {
            // Right next to the window it explains: its left edge just past the window's right edge, top or bottom aligned.
            Canvas.ForceUpdateCanvases();
            Vector3[] corners = new Vector3[4];
            FullSizeCorners(beside, corners); // overlay canvas: world = screen pixels (0 = bottom-left, 2 = top-right)
            RectTransform canvasRect = canvasRoot.GetComponent<RectTransform>();
            float toUnits = canvasRect.rect.width / Mathf.Max(1f, Screen.width);
            float textHeightB = Mathf.Ceil(label.GetPreferredValues(message, width - 60f, 0f).y);
            boxRect.sizeDelta = new Vector2(width, Mathf.Max(minHeight, textHeightB + 130f));   // minHeight keeps a series of tips the same size
            float x = corners[2].x * toUnits + 24f;
            float room = canvasRect.rect.width - 10f - x;
            if (room < width)   // not enough room to the right of the window (high UI scale): the box gets narrower instead of covering the window
            {
                width = Mathf.Max(260f, room);
                float th = Mathf.Ceil(label.GetPreferredValues(message, width - (small ? 36f : 60f), 0f).y);
                boxRect.sizeDelta = new Vector2(width, Mathf.Max(minHeight, th + 130f));
                x = Mathf.Min(x, canvasRect.rect.width - width - 10f);
            }
            boxRect.anchorMin = boxRect.anchorMax = new Vector2(0f, 0f);
            if (besideBottom)
            {
                boxRect.pivot = new Vector2(0f, 0f);
                boxRect.anchoredPosition = new Vector2(x, corners[0].y * toUnits);
            }
            else
            {
                boxRect.pivot = new Vector2(0f, 1f);
                boxRect.anchoredPosition = new Vector2(x, corners[2].y * toUnits);
            }
            besideTarget = beside;
            besideBottomFlag = besideBottom;
            besideRight = corners[2].x;
        }
        else if (fixedSize.x > 0f && fixedSize.y > 0f && PixelNotice.SideAnchor != null && PixelNotice.SideAnchor.gameObject.activeInHierarchy && PixelNotice.SideWidth > 40f)
        {
            // To the right of the shop, filling the same height as the shop (and its currency panel on the other side).
            Canvas.ForceUpdateCanvases();
            Vector3[] shopCorners = new Vector3[4];
            FullSizeCorners(PixelNotice.SideAnchor, shopCorners);
            RectTransform canvasRect2 = canvasRoot.GetComponent<RectTransform>();
            float toUnits2 = canvasRect2.rect.width / Mathf.Max(1f, Screen.width);
            float sideWidth = PixelNotice.SideWidth;
            float shopHeight = (shopCorners[2].y - shopCorners[0].y) * toUnits2;
            float sx = shopCorners[2].x * toUnits2 + Mathf.Max(0f, PixelNotice.SideGap);
            sx = Mathf.Min(sx, canvasRect2.rect.width - sideWidth - 10f);
            boxRect.anchorMin = boxRect.anchorMax = new Vector2(0f, 0f);
            boxRect.pivot = new Vector2(0f, 0f);
            boxRect.sizeDelta = new Vector2(sideWidth, shopHeight);
            boxRect.anchoredPosition = new Vector2(sx, shopCorners[0].y * toUnits2);
        }
        else if (fixedSize.x > 0f && fixedSize.y > 0f)
        {
            // Same size as the shop, centred on screen.
            boxRect.anchorMin = boxRect.anchorMax = boxRect.pivot = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = fixedSize;
            boxRect.anchoredPosition = Vector2.zero;
        }
        else
        {
            float textHeight = Mathf.Ceil(label.GetPreferredValues(message, width - (small ? 36f : 60f), 0f).y);
            float bar = PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f;
            boxRect.sizeDelta = new Vector2(width, Mathf.Max(minHeight, textHeight + (small ? 100f : 130f)));   // (minHeight keeps a series of tips - the tutorials - the same size, so Got it never moves)
            if (nearLog)
            {
                // Bottom left, right next to the event log.
                boxRect.anchorMin = boxRect.anchorMax = boxRect.pivot = new Vector2(0f, 0f);
                boxRect.anchoredPosition = new Vector2(PixelHints.LogRightEdge, bar + 24f);
            }
            else if (aboveCombo)
            {
                // At the bottom, just above the combo meter.
                PixelCombo combo = PixelFind.First<PixelCombo>();
                float above = combo != null ? combo.MeterTopOffset : bar + 140f;
                boxRect.anchorMin = boxRect.anchorMax = boxRect.pivot = new Vector2(0.5f, 0f);
                boxRect.anchoredPosition = new Vector2(0f, above);
            }
            else
            {
                boxRect.anchorMin = boxRect.anchorMax = boxRect.pivot = new Vector2(0.5f, 1f);
                boxRect.anchoredPosition = new Vector2(0f, -(bar + 24f));
            }
        }

        canvasRoot.SetActive(true);
        PixelPop.Show(boxRect.gameObject);   // a box that was still shrinking away grows back for the new tip
        timed = seconds > 0f;
        timer = seconds;
        if (backRect != null) backRect.gameObject.SetActive(backCallback != null);
        if (skipRect != null) skipRect.gameObject.SetActive(skipCallback != null);
        if (muteRect != null) muteRect.anchoredPosition = new Vector2(backCallback != null ? 62f : 10f, -10f);   // (next to the back arrow, or in its place)
        if (!timed)
        {
            label.ForceMeshUpdate();
            typeTotal = label.textInfo.characterCount;
            typeShown = 0f;
            typing = typeTotal > 0;
            label.maxVisibleCharacters = typing ? 0 : int.MaxValue;
        }
        else FinishTyping();
        if (followPoint != null) PlaceNearPoint();
        if (freezeTime && Time.timeScale > 0f)
        {
            savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            froze = true;
        }
    }

    private System.Func<Vector2?> followPoint;
    private bool froze;
    private float savedTimeScale = 1f;

    private void Unfreeze()
    {
        if (!froze) return;
        froze = false;
        if (Time.timeScale <= 0f) Time.timeScale = savedTimeScale;
    }

    /// <summary>Puts the box beside the followed screen point (right of it, or left near the screen's right edge), kept on screen.</summary>
    private void PlaceNearPoint()
    {
        Vector2? point = followPoint != null ? followPoint() : null;
        if (!point.HasValue) return;
        RectTransform canvasRect = canvasRoot.GetComponent<RectTransform>();
        float toUnits = canvasRect.rect.width / Mathf.Max(1f, Screen.width);
        Vector2 p = point.Value * toUnits;
        float w = boxRect.sizeDelta.x, h = boxRect.sizeDelta.y;
        float gap = 90f; // clear of the ghost itself
        bool right = p.x + gap + w <= canvasRect.rect.width - 10f;
        boxRect.anchorMin = boxRect.anchorMax = Vector2.zero;
        boxRect.pivot = new Vector2(right ? 0f : 1f, 0.5f);
        float y = Mathf.Clamp(p.y, h * 0.5f + 10f, canvasRect.rect.height - h * 0.5f - 10f);
        boxRect.anchoredPosition = new Vector2(right ? p.x + gap : p.x - gap, y);
    }

    private void Awake()
    {
        // Escape closes the tip box (and drops the tips still waiting behind it), like pressing Got it.
        PixelWindows.Register(this, 140, () => IsOpen, () => { PixelHints.ClearQueue(); Close(); });
    }

    private void OnDestroy() { Unfreeze(); PixelWindows.Unregister(this); }

    /// <summary>The window's corners at full size: a window that is still growing in (PixelPop) would otherwise put the box on top of it.</summary>
    private static void FullSizeCorners(RectTransform window, Vector3[] corners)
    {
        // At full size, but shrunk by the fit a PixelPop window gets when it is bigger than the screen (high UI scale).
        Vector3 scale = window.localScale;
        PixelPop pop = window.GetComponent<PixelPop>();
        window.localScale = Vector3.one * (pop != null ? pop.Fit : 1f);
        window.GetWorldCorners(corners);
        window.localScale = scale;
    }

    public bool IsOpen => canvasRoot != null && canvasRoot.activeSelf && PixelPop.IsOpen(boxRect.gameObject);

    private System.Action closedCallback;

    public void Close()
    {
        bool wasOpen = canvasRoot != null && canvasRoot.activeSelf;
        if (canvasRoot != null) PixelPop.Hide(boxRect.gameObject, canvasRoot);
        Unfreeze();
        System.Action callback = closedCallback;
        closedCallback = null;
        backCallback = null;
        skipCallback = null;
        FinishTyping();
        if (wasOpen) callback?.Invoke();
    }

    // The window the box sits beside is tracked: if it was still opening / laying out when the box appeared (so it was a different size then), the box moves to its real edge.
    private RectTransform besideTarget;
    private bool besideBottomFlag;
    private float besideRight = -1f;

    private void TrackBesideWindow()
    {
        if (besideTarget == null || !besideTarget.gameObject.activeInHierarchy) { besideTarget = null; return; }
        Vector3[] corners = new Vector3[4];
        FullSizeCorners(besideTarget, corners);
        if (Mathf.Abs(corners[2].x - besideRight) < 1f) return;
        besideRight = corners[2].x;
        RectTransform canvasRect = canvasRoot.GetComponent<RectTransform>();
        float toUnits = canvasRect.rect.width / Mathf.Max(1f, Screen.width);
        float x = Mathf.Min(corners[2].x * toUnits + 24f, canvasRect.rect.width - boxRect.rect.width - 10f);
        boxRect.anchoredPosition = new Vector2(x, (besideBottomFlag ? corners[0].y : corners[2].y) * toUnits);
    }

    private void Update()
    {
        if (typing && label != null)
        {
            int wasShown = (int)typeShown;
            typeShown += Time.unscaledDeltaTime * TypeCharsPerSecond;
            PixelTalk.Typed(label, wasShown, Mathf.Min(typeTotal, (int)typeShown));   // talking blips
            if (typeShown >= typeTotal) FinishTyping();
            else label.maxVisibleCharacters = (int)typeShown;
        }
        if (canvasRoot != null && canvasRoot.activeSelf) TrackBesideWindow();
        if (followPoint != null && canvasRoot != null && canvasRoot.activeSelf) PlaceNearPoint();
        if (!timed || canvasRoot == null || !canvasRoot.activeSelf) return;
        timer -= Time.unscaledDeltaTime;
        if (timer <= 0f) Close();
    }
}
