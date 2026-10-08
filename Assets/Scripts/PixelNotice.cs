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
    }

    private static PixelNoticeBox box;

    /// <summary>Shows a message. 'seconds' = 0 keeps it until the player closes it.</summary>
    public static void Show(string message, float seconds = 0f, System.Action onClosed = null)
    {
        if (box == null) box = new GameObject("Pixel Notice").AddComponent<PixelNoticeBox>();
        box.Open(message, seconds, onClosed);
    }

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

    private GameObject canvasRoot;
    private TMP_Text label;
    private RectTransform boxRect;
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
    }

    public void Open(string message, float seconds, System.Action onClosed = null)
    {
        closedCallback = onClosed; // a tip replaced by a new one never runs the old callback
        if (canvasRoot == null) Build();

        label.text = message;
        float textHeight = Mathf.Ceil(label.GetPreferredValues(message, Width - 60f, 0f).y);
        float bar = PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f;
        boxRect.sizeDelta = new Vector2(Width, textHeight + 130f);
        boxRect.anchoredPosition = new Vector2(0f, -(bar + 24f));

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
