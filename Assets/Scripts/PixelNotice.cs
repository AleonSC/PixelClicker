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
    }

    private static PixelNoticeBox box;

    /// <summary>Shows a message. 'seconds' = 0 keeps it until the player closes it.</summary>
    /// <summary>'compact' = the small text-sized box at the top of the screen (tutorial tips next to the buttons), even if a fixed size is set.</summary>
    /// <summary>'small' = a much smaller box (narrow, small text and button) at the top of the screen: for quick "this just happened" tips.</summary>
    /// <summary>'aboveCombo' = a small box at the bottom of the screen, just above the combo meter.</summary>
    public static void Show(string message, float seconds = 0f, System.Action onClosed = null, bool compact = false, bool small = false, bool aboveCombo = false)
    {
        if (box == null) box = new GameObject("Pixel Notice").AddComponent<PixelNoticeBox>();
        box.Open(PixelKeys.Replace(message), seconds, onClosed, compact, small || aboveCombo, aboveCombo); // {key:...} placeholders show the player's keys
    }

    /// <summary>A fixed box size (canvas units) for every tip box, shown centred on screen. Vector2.zero = size to the text, at the top.</summary>
    public static Vector2 FixedSize { get; set; }

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
        box.GetComponent<Image>().color = new Color(0.1f, 0.22f, 0.32f, 0.97f);
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
        ok.onClick.AddListener(Close);
        okRect = okr;
        okLabel = ok.GetComponentInChildren<TMP_Text>();
    }

    public void Open(string message, float seconds, System.Action onClosed = null, bool compact = false, bool small = false, bool aboveCombo = false)
    {
        closedCallback = onClosed; // a tip replaced by a new one never runs the old callback
        if (canvasRoot == null) Build();

        // The small box: narrow, small text, small button.
        float width = small ? SmallWidth : Width;
        float textSize = small ? 22f : 30f;
        label.fontSize = textSize;
        label.rectTransform.offsetMin = small ? new Vector2(18f, 62f) : new Vector2(30f, 90f);
        label.rectTransform.offsetMax = small ? new Vector2(-18f, -12f) : new Vector2(-30f, -20f);
        okRect.sizeDelta = small ? new Vector2(150f, 44f) : new Vector2(200f, 60f);
        okRect.anchoredPosition = new Vector2(0f, small ? 10f : 18f);
        if (okLabel != null) okLabel.fontSize = small ? 22f : 30f;

        label.text = message;
        Vector2 fixedSize = compact || small ? Vector2.zero : PixelNotice.FixedSize;
        if (fixedSize.x > 0f && fixedSize.y > 0f)
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
            boxRect.sizeDelta = new Vector2(width, textHeight + (small ? 84f : 130f));
            if (aboveCombo)
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
        timed = seconds > 0f;
        timer = seconds;
    }

    public bool IsOpen => canvasRoot != null && canvasRoot.activeSelf;

    private System.Action closedCallback;

    public void Close()
    {
        bool wasOpen = canvasRoot != null && canvasRoot.activeSelf;
        if (canvasRoot != null) canvasRoot.SetActive(false);
        System.Action callback = closedCallback;
        closedCallback = null;
        if (wasOpen) callback?.Invoke();
    }

    private void Update()
    {
        if (!timed || canvasRoot == null || !canvasRoot.activeSelf) return;
        timer -= Time.unscaledDeltaTime;
        if (timer <= 0f) Close();
    }
}
