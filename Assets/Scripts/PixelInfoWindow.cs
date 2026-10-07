using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Info window (press I): the purchased upgrades with what they do, and the recent events (click one to reopen its tip).
/// Events come from <see cref="PixelHints"/>. Added by PixelHints; opens with a key and is closed with Escape or its X.
/// </summary>
public class PixelInfoWindow : MonoBehaviour
{
    [Header("Window")]
    [Tooltip("Key that opens and closes the Info window.")]
    [SerializeField] private KeyCode openKey = KeyCode.I;

    [Tooltip("Window size (canvas units).")]
    [SerializeField] private Vector2 windowSize = new Vector2(900f, 760f);

    [Tooltip("Window title.")]
    [SerializeField] private string titleText = "Info";

    [Tooltip("Heading above the purchased upgrades.")]
    [SerializeField] private string upgradesHeading = "Purchased Upgrades";

    [Tooltip("Heading above the recent events.")]
    [SerializeField] private string eventsHeading = "Recent Events (click one to reopen its tip)";

    [Tooltip("Shown when nothing has been bought yet.")]
    [SerializeField] private string noUpgradesText = "Nothing bought yet.";

    [Tooltip("Shown when no event has happened yet.")]
    [SerializeField] private string noEventsText = "Nothing has happened yet.";

    [Header("Look")]
    [Tooltip("Background colour of the window.")]
    [SerializeField] private Color windowColor = new Color(0.08f, 0.1f, 0.14f, 0.97f);

    [Tooltip("Colour of headings.")]
    [SerializeField] private Color headingColor = new Color(1f, 0.93f, 0.55f, 1f);

    [Tooltip("Colour of upgrade names / event lines.")]
    [SerializeField] private Color nameColor = Color.white;

    [Tooltip("Colour of upgrade descriptions.")]
    [SerializeField] private Color descriptionColor = new Color(0.75f, 0.8f, 0.88f, 1f);

    [Tooltip("Colour of event rows.")]
    [SerializeField] private Color eventRowColor = new Color(0.2f, 0.3f, 0.45f, 1f);

    [Tooltip("Font size of names / events.")]
    [SerializeField] private float fontSize = 28f;

    [Tooltip("Font size of descriptions.")]
    [SerializeField] private float descriptionFontSize = 22f;

    [Tooltip("Font size of headings.")]
    [SerializeField] private float headingFontSize = 32f;

    private GameObject canvasRoot;
    private RectTransform windowRect;
    private ScrollRect scroll;
    private RectTransform content;
    private GameObject barObject;
    private TMP_FontAsset font;
    private PixelShop shop;
    private bool open;

    public bool IsOpen => open;

    private void Awake()
    {
        PixelWindows.Register(this, 25, () => open, Close);
    }

    private void OnDestroy()
    {
        PixelWindows.Unregister(this);
        if (canvasRoot != null) Destroy(canvasRoot);
    }

    private void Update()
    {
        if (PixelPauseMenu.IsPaused) return;
        UnityEngine.EventSystems.EventSystem es = UnityEngine.EventSystems.EventSystem.current;
        if (es != null && es.currentSelectedGameObject != null && es.currentSelectedGameObject.GetComponent<TMP_InputField>() != null) return;
        if (KeyPressed()) { if (open) Close(); else Open(); }
    }

    private bool KeyPressed()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.iKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(openKey);
#endif
    }

    public void Open()
    {
        if (canvasRoot == null) Build();
        open = true;
        canvasRoot.SetActive(true);
        Rebuild();
    }

    public void Close()
    {
        open = false;
        if (canvasRoot != null) canvasRoot.SetActive(false);
    }

    private void Build()
    {
        PixelClicker clicker = PixelFind.First<PixelClicker>();
        font = clicker != null ? clicker.UIFont : null;
        PixelUIKit.EnsureEventSystem();
        canvasRoot = PixelUIKit.CreateCanvas("Pixel Info Window", 70, new Vector2(1920f, 1080f), true);
        canvasRoot.transform.SetParent(transform, false);

        GameObject win = new GameObject("Window", typeof(RectTransform), typeof(Image));
        win.transform.SetParent(canvasRoot.transform, false);
        win.GetComponent<Image>().color = windowColor;
        windowRect = win.GetComponent<RectTransform>();
        windowRect.anchorMin = windowRect.anchorMax = windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.sizeDelta = windowSize;

        TMP_Text title = PixelUIKit.CreateText(font, win.transform, "Title", titleText, headingFontSize + 4f,
                                               TextAlignmentOptions.Center, FontStyles.Bold, Color.white);
        RectTransform tr = title.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f); tr.pivot = new Vector2(0.5f, 1f);
        tr.anchoredPosition = new Vector2(0f, -12f);
        tr.sizeDelta = new Vector2(0f, headingFontSize * 1.6f);

        Button close = PixelUIKit.CreateButton(font, win.transform, "Close", "X", new Vector2(56f, 56f),
                                               new Color(0.5f, 0.2f, 0.2f, 1f), Color.white, 30f);
        RectTransform cr = close.GetComponent<RectTransform>();
        cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(1f, 1f);
        cr.anchoredPosition = new Vector2(-12f, -12f);
        close.onClick.AddListener(Close);

        scroll = PixelUIKit.CreateScrollView(win.transform, "List", new Color(1f, 1f, 1f, 0.35f), 10f, 40f, out content, out barObject);
        RectTransform vr = scroll.GetComponent<RectTransform>();
        vr.anchorMin = Vector2.zero; vr.anchorMax = Vector2.one;
        vr.offsetMin = new Vector2(24f, 20f);
        vr.offsetMax = new Vector2(-24f, -(headingFontSize * 1.6f + 28f));
    }

    private float AddText(string text, float size, FontStyles style, Color color, float y, float width)
    {
        TMP_Text t = PixelUIKit.CreateText(font, content, "Text", text, size, TextAlignmentOptions.TopLeft, style, color);
        t.overflowMode = TextOverflowModes.Overflow;
        float h = Mathf.Ceil(t.GetPreferredValues(text, width, 0f).y) + 4f;
        RectTransform r = t.rectTransform;
        r.anchorMin = new Vector2(0f, 1f); r.anchorMax = new Vector2(1f, 1f); r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = new Vector2(8f, -y);
        r.sizeDelta = new Vector2(-24f, h);
        return h;
    }

    private void Rebuild()
    {
        for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
        float width = windowSize.x - 48f - 40f;
        float y = 4f;

        y += AddText(upgradesHeading, headingFontSize, FontStyles.Bold, headingColor, y, width) + 6f;
        if (shop == null) shop = PixelFind.First<PixelShop>();
        int shown = 0;
        if (shop != null)
        {
            for (int i = 0; i < shop.PackCount; i++)
            {
                if (!shop.GetOwnedPackInfo(i, out string name, out string description)) continue;
                shown++;
                y += AddText(name, fontSize, FontStyles.Bold, nameColor, y, width);
                if (!string.IsNullOrEmpty(description)) y += AddText(description, descriptionFontSize, FontStyles.Normal, descriptionColor, y, width);
                y += 8f;
            }
        }
        if (shown == 0) y += AddText(noUpgradesText, descriptionFontSize, FontStyles.Italic, descriptionColor, y, width) + 8f;

        y += 14f;
        y += AddText(eventsHeading, headingFontSize, FontStyles.Bold, headingColor, y, width) + 6f;
        IReadOnlyList<PixelHints.Entry> history = PixelHints.History;
        if (history == null || history.Count == 0)
        {
            y += AddText(noEventsText, descriptionFontSize, FontStyles.Italic, descriptionColor, y, width);
        }
        else
        {
            for (int i = history.Count - 1; i >= 0; i--) // newest first
            {
                PixelHints.Entry entry = history[i];
                Button b = PixelUIKit.CreateButton(font, content, "Event", entry.text, new Vector2(width, fontSize * 1.9f),
                                                   eventRowColor, nameColor, fontSize - 2f);
                RectTransform br = b.GetComponent<RectTransform>();
                br.anchorMin = br.anchorMax = br.pivot = new Vector2(0f, 1f);
                br.anchoredPosition = new Vector2(8f, -y);
                if (entry.open != null)
                {
                    Action act = entry.open;
                    b.onClick.AddListener(() => { Close(); act(); });
                }
                else b.interactable = false;
                y += fontSize * 1.9f + 6f;
            }
        }

        float viewHeight = windowSize.y - (headingFontSize * 1.6f + 28f) - 20f;
        PixelUIKit.UpdateScrollView(scroll, barObject, y + 10f, viewHeight);
    }
}
