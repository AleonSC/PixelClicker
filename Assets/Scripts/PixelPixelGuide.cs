// ---- System notes (moved here from CLAUDE.md) ----
// PixelPixelGuide.cs / PixelGuideTexts.cs
//   Pixel Guide (added by PixelClicker.Awake): a docked 'Pixel Guide' button on the right edge directly under Toggles (PixelHud.Dock
//   with a negative sideOffsetY; shows once two pixel types are unlocked, hideUntilSecondPixel) that slides out a window like Toggles
//   (same spot, docks beside the open shop with the Currency panel's width and the shop's height via FitBesideShop; opening one closes
//   the other: PixelToggles.CloseWindowIfOpen / PixelPixelGuide.CloseWindowIfOpen; Escape closes it, PixelWindows 15). It lists every
//   pixel IN THE ROTATION (PixelClicker.InRotation(i) = can spawn now and not left out by the skill pack in play; found Dragon Cubes
//   too; SpawnShare(i) = its share of spawns) as a row: a turning real-look 3D picture (the shared PixelTierPicture studio of that type,
//   re-rendered only for rows in view), the name in its colour, a line 'Pays N - clicks - X%
//   of pixels' and an orange '!' box whose tooltip is PixelGuideTexts.Describe(type, tier) (the exact mechanics + what its old pixel
//   does). Special / hold blocks (PixelBlockRules) are tagged in the sub line and get their role sentences from PixelBlockRules.GuideNotes automatically. A new pixel type needs a case in PixelGuideTexts.Mechanics (and OldPixel if its old pixel behaves differently).
// ---------------------------------------------------
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Pixel Guide: a docked button on the right edge (under Toggles) that slides out a window like the Toggles window (and docks with the shop the same
/// way). It lists every pixel in the current rotation - the ones that can come up on the cube right now - each with a turning 3D picture of the real pixel
/// and an orange '!' box whose tooltip explains exactly what the pixel does. It is a standard Fixed window (PixelUIKit.CreateWindow), anchored at the
/// middle of the right edge (or beside the open shop); its own orange '!' sits in the left header slot.
/// </summary>
public class PixelPixelGuide : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The PixelClicker whose pixels are listed. Found automatically if left empty.")]
    [SerializeField] private PixelClicker clicker;

    [Header("Button")]
    [Tooltip("Text on the button under Toggles.")]
    [SerializeField] private string buttonText = "Rotation";

    [Tooltip("Button colour (the Pixel Guide's own teal, kept as a plain colour rather than a PixelUITheme role).")]
    [SerializeField] private Color buttonColor = new Color(0.2f, 0.55f, 0.6f, 1f);

    [Tooltip("Keep the button hidden until a second pixel type is unlocked.")]
    [SerializeField] private bool hideUntilSecondPixel = true;

    [Header("Window")]
    [Tooltip("Text of the orange ! box at the top left of the window (what the window is for).")]
    [TextArea(2, 6)]
    [SerializeField] private string infoText = "<b>Pixel Rotation</b>\nEvery pixel type that can come up on the cube right now - the ones in your current rotation - with a turning 3D picture, what it pays and how often it shows up.\nNew pixels appear here as you unlock them. Hover a row's orange ! to see exactly how that pixel works.";

    [Tooltip("Window title.")]
    [SerializeField] private string windowTitle = "Pixel Rotation";

    [Tooltip("Window size (canvas units) when it stands alone.")]
    [SerializeField] private Vector2 windowSize = new Vector2(640f, 720f);

    [Tooltip("Height of one pixel row.")]
    [SerializeField] private float rowHeight = 118f;

    [Tooltip("Size of the turning 3D picture on each row.")]
    [SerializeField] private float pictureSize = 96f;

    [Tooltip("Seconds per full turn of the 3D pictures. 0 = 8.")]
    [SerializeField] private float spinSeconds = 0f;

    [Tooltip("Text shown when no pixel is in the rotation.")]
    [SerializeField] private string emptyText = "No pixels yet.";


    private Color textColor => PixelUITheme.TextColor;   // (the theme: PixelUITheme)

    [Header("Canvas")]
    [Tooltip("Sorting order of the canvas (just above the Toggles window).")]
    [SerializeField] private int sortingOrder = 131;

    private static PixelPixelGuide instance;

    private GameObject canvasRoot, openObject, windowObject;
    private PixelDockedButton dock;
    private CanvasGroup buttonGroup;
    private ScrollRect scroll;
    private RectTransform content;
    private GameObject bar, emptyObject;
    private PixelShop shopRef;
    private bool built;
    private float refreshTimer;
    private string rowsKey = "";

    private class Row
    {
        public RectTransform rect;
        public RawImage picture;
        public int tier;
    }

    private readonly List<Row> rows = new List<Row>();

    // The tooltip shared by every row's '!' box.
    private RectTransform tipRect;
    private TMP_Text tipText;
    private System.Func<string> hoverTip;

    private TMP_FontAsset Font => clicker != null ? clicker.UIFont : null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; }

    /// <summary>Closes the window if it is open (Toggles takes the same spot on the right).</summary>
    public static void CloseWindowIfOpen()
    {
        if (instance != null && instance.windowObject != null && PixelPop.IsOpen(instance.windowObject)) instance.Close();
    }

    private void Awake()
    {
        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
        if (clicker == null) { enabled = false; return; }
        instance = this;
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

    private bool ShowButton()
    {
        if (!hideUntilSecondPixel) return true;
        int unlocked = 0;
        foreach (PixelClicker.PixelTier t in clicker.Tiers) if (t.unlocked && ++unlocked >= 2) return true;
        return false;
    }

    // ------------------------------------------------------------------
    // Building the UI
    // ------------------------------------------------------------------

    private void BuildUI()
    {
        canvasRoot = PixelUIKit.CreateCanvas("PixelGuide Canvas", sortingOrder, new Vector2(1920f, 1080f), true);

        // The button, docked on the middle of the right edge directly under the Toggles button.
        Button open = PixelUIKit.CreateButton(Font, canvasRoot.transform, "Pixel Guide Button", buttonText, new Vector2(230f, 64f), buttonColor, Color.white, 30f);
        PixelHud hud = PixelHud.Ensure(gameObject);
        // Bottom-right corner, beside the Toggles tab and its My Window button: [Pixel Guide][My Window][Toggles].
        float guideOffset = hud.ButtonSize.x + hud.CornerGap;
        hud.Dock(open.GetComponent<RectTransform>(), new Vector2(1f, 0f), () => windowObject != null && windowObject.activeSelf, 0f, 1f, guideOffset);
        open.onClick.AddListener(Toggle);
        openObject = open.gameObject;
        dock = openObject.GetComponent<PixelDockedButton>();
        buttonGroup = openObject.AddComponent<CanvasGroup>();

        // The window: the standard Fixed window (PixelUIKit.CreateWindow: title, grey X, PixelPop). The builder centres it; this one is put
        // back in the spot the button slides out to, at the middle of the right edge (FitBesideShop moves it next to the open shop).
        PixelWindowParts parts = PixelUIKit.CreateWindow(Font, canvasRoot.transform, "Pixel Guide Window", windowTitle, windowSize,
                                                         PixelUIKit.WindowKind.Fixed, Close);
        windowObject = parts.Root;
        RectTransform wr = parts.Rect;
        wr.anchorMin = wr.anchorMax = wr.pivot = new Vector2(1f, 0.5f);
        wr.anchoredPosition = new Vector2(-(hud.SideShownInset), 0f);

        PixelShop layoutShop = PixelFind.First<PixelShop>();
        float headerH = parts.HeaderHeight;
        float sidePad = layoutShop != null ? layoutShop.PanelPaddingValue : 20f;
        float listTop = headerH + 10f;
        PixelMenuFx.AddWindowLine(windowObject.transform, headerH - 2f);

        // The orange ! (what this window is for) in the standard left header slot, opposite the X (the shared info badge: a hover tip, not a button).
        parts.PlaceInHeaderSlot(CreateInfoBox(windowObject.transform, PixelUIKit.WindowCloseSize, () => infoText), true);

        scroll = PixelUIKit.CreateScrollView(windowObject.transform, "Pixel List", new Color(1f, 1f, 1f, 0.35f), 12f, rowHeight, out content, out bar);
        PixelScrollBarFade barFade = bar != null ? bar.GetComponent<PixelScrollBarFade>() : null;
        if (barFade != null) barFade.hideBar = true;
        RectTransform vr = scroll.GetComponent<RectTransform>();
        vr.anchorMin = Vector2.zero;
        vr.anchorMax = Vector2.one;
        vr.offsetMin = new Vector2(sidePad, sidePad);
        vr.offsetMax = new Vector2(-sidePad, -listTop);

        TMP_Text empty = PixelUIKit.CreateText(Font, windowObject.transform, "Empty", emptyText.ToUpperInvariant(), 27f, TextAlignmentOptions.Center, FontStyles.Bold,
                                               new Color(textColor.r, textColor.g, textColor.b, 0.6f));
        PixelUIKit.Stretch(empty.rectTransform);
        emptyObject = empty.gameObject;

        BuildTip();
        windowObject.SetActive(false);
        PixelWindows.Register(this, 15, () => windowObject != null && PixelPop.IsOpen(windowObject), Close);
    }

    private void BuildTip()
    {
        GameObject go = new GameObject("Pixel Guide Tip", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(canvasRoot.transform, false);
        Image bg = go.GetComponent<Image>();
        bg.color = new Color(0.05f, 0.05f, 0.08f, 0.97f);
        bg.raycastTarget = false;
        PixelUIKit.StyleBox(bg);
        tipRect = go.GetComponent<RectTransform>();
        tipRect.anchorMin = tipRect.anchorMax = new Vector2(0.5f, 0.5f);
        tipRect.pivot = new Vector2(0f, 1f);
        tipText = PixelUIKit.CreateText(Font, go.transform, "Text", "", 22f, TextAlignmentOptions.TopLeft, FontStyles.Normal, textColor);
        tipText.richText = true;
        tipText.raycastTarget = false;
        RectTransform t = tipText.rectTransform;
        t.anchorMin = t.anchorMax = new Vector2(0f, 1f);
        t.pivot = new Vector2(0f, 1f);
        t.anchoredPosition = new Vector2(14f, -10f);
        go.SetActive(false);
    }

    private void UpdateTip()
    {
        if (tipRect == null) return;
        string text = windowObject != null && windowObject.activeSelf && hoverTip != null ? hoverTip() : null;
        bool show = !string.IsNullOrEmpty(text);
        if (tipRect.gameObject.activeSelf != show) tipRect.gameObject.SetActive(show);
        if (!show) return;
        const float wrap = 480f;
        PixelUIKit.SetText(tipText, text);
        Vector2 pref = tipText.GetPreferredValues(text, wrap, 0f);
        tipText.rectTransform.sizeDelta = new Vector2(wrap, pref.y);
        Vector2 size = new Vector2(wrap + 28f, pref.y + 20f);
        tipRect.sizeDelta = size;
        RectTransform canvasRect = canvasRoot.GetComponent<RectTransform>();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, PixelInput.PointerPosition(), null, out Vector2 local);
        PixelUIKit.PlaceTooltip(tipRect, canvasRect, local);
        tipRect.SetAsLastSibling();
    }

    private RectTransform CreateInfoBox(Transform parent, float size, System.Func<string> source) =>
        PixelUIKit.CreateInfoBadge(Font, parent, size, () => hoverTip = source, () => { if (hoverTip == source) hoverTip = null; });   // (the shared orange '!')

    private void Toggle()
    {
        if (PixelPop.IsOpen(windowObject)) Close();
        else
        {
            PixelToggles.CloseWindowIfOpen();   // they share the right edge
            PixelPop.Show(windowObject);
            refreshTimer = 0f;
            Refresh();
        }
    }

    private void Close() => PixelPop.Hide(windowObject);

    // ------------------------------------------------------------------
    // The list
    // ------------------------------------------------------------------

    private static string ShortName(string name)
    {
        return name != null && name.EndsWith(" Pixels") ? name.Substring(0, name.Length - 7) : name;
    }

    private string SubLine(int index)
    {
        PixelClicker.PixelTier t = clicker.Tiers[index];
        StringBuilder sb = new StringBuilder();
        if (t.rareDrop) sb.Append("Pays 1");
        else if (t.randomPayout) sb.Append("Pays ").Append(t.payoutMin).Append("-").Append(t.payoutMax);
        else sb.Append("Pays ").Append(PixelClicker.FormatNumber(t.amountPerClick));
        if (t.randomClicks) sb.Append("  -  ").Append(t.randomClicksMin).Append("-").Append(t.randomClicksMax).Append(" clicks");
        else if (t.clicksToCollect > 1) sb.Append("  -  ").Append(t.clicksToCollect).Append(" clicks");
        float share = clicker.SpawnShare(index);
        bool isSpecial = PixelBlockRules.TryGetSpecial(t.type, out PixelBlockRules.SpecialRule special);
        if (isSpecial) sb.Append("  -  SPECIAL, ").Append(special.chance >= 0.1f ? Mathf.RoundToInt(special.chance * 100f).ToString() : (special.chance * 100f).ToString("0.#")).Append("% replaces a block").Append(special.hasBoost ? " (up to " + Mathf.RoundToInt(special.maxChance * 100f) + "% with " + special.boostBlock.ToString().ToLowerInvariant() + "s on the field)" : "");
        if (PixelBlockRules.TryGetHold(t.type, out PixelBlockRules.HoldRule _)) sb.Append("  -  HOLD block");
        if (t.stallsHold) sb.Append("  -  STALL");
        if (t.rareDrop) sb.Append("  -  rare drop");
        else if (share > 0f && !isSpecial) sb.Append("  -  ").Append(share >= 0.1f ? Mathf.RoundToInt(share * 100f).ToString() : (share * 100f).ToString("0.#")).Append("% of pixels");
        return sb.ToString();
    }

    private void Refresh()
    {
        if (content == null) return;
        PixelBlockRules.EnsureActiveSpecialTiers(clicker);   // special blocks of the skill in play are listed from the start
        // Which pixels, and their shares (a share changes when something unlocks).
        StringBuilder key = new StringBuilder();
        List<int> list = new List<int>();
        for (int i = 0; i < clicker.Tiers.Length; i++)
        {
            if (!clicker.InRotation(i)) continue;
            list.Add(i);
            key.Append(i).Append(':').Append(Mathf.RoundToInt(clicker.SpawnShare(i) * 1000f)).Append(';');
        }
        string newKey = key.ToString();
        if (newKey != rowsKey)
        {
            rowsKey = newKey;
            foreach (Row r in rows) if (r.rect != null) Destroy(r.rect.gameObject);
            rows.Clear();
            for (int n = 0; n < list.Count; n++) rows.Add(BuildRow(list[n], n));
        }
        emptyObject.SetActive(rows.Count == 0);
        float contentHeight = rows.Count * (rowHeight + 8f) - 8f;
        float viewHeight = scroll.GetComponent<RectTransform>().rect.height;
        PixelUIKit.UpdateScrollView(scroll, bar, Mathf.Max(contentHeight, 0f), viewHeight);
    }

    private Row BuildRow(int tierIndex, int order)
    {
        PixelClicker.PixelTier t = clicker.Tiers[tierIndex];
        GameObject go = new GameObject("Pixel " + t.type, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(content, false);
        Image bg = go.GetComponent<Image>();
        bg.color = new Color(0.17f, 0.17f, 0.21f, 1f);
        PixelUIKit.StyleBox(bg);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(0f, rowHeight);
        rt.anchoredPosition = new Vector2(0f, -order * (rowHeight + 8f));

        // The turning 3D picture, in a sunken frame.
        GameObject frame = new GameObject("Picture Frame", typeof(RectTransform), typeof(Image));
        frame.transform.SetParent(go.transform, false);
        Image frameImage = frame.GetComponent<Image>();
        frameImage.color = new Color(0.07f, 0.08f, 0.11f, 1f);
        frameImage.raycastTarget = false;
        PixelUIKit.StyleBox(frameImage, true);
        RectTransform fr = frame.GetComponent<RectTransform>();
        fr.anchorMin = fr.anchorMax = fr.pivot = new Vector2(0f, 0.5f);
        fr.sizeDelta = new Vector2(pictureSize + 12f, pictureSize + 12f);
        fr.anchoredPosition = new Vector2(12f, 0f);

        GameObject picObj = new GameObject("Picture", typeof(RectTransform), typeof(RawImage));
        picObj.transform.SetParent(frame.transform, false);
        RawImage pic = picObj.GetComponent<RawImage>();
        pic.raycastTarget = false;
        pic.texture = StudioTexture(tierIndex);
        RectTransform pr = picObj.GetComponent<RectTransform>();
        pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(0.5f, 0.5f);
        pr.sizeDelta = new Vector2(pictureSize, pictureSize);

        float infoSize = 44f;
        float textLeft = 12f + pictureSize + 12f + 14f;

        TMP_Text name = PixelUIKit.CreateText(Font, go.transform, "Name", ShortName(t.displayName), 32f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, t.color);
        name.raycastTarget = false;
        PixelUIKit.Caps(name);
        name.enableAutoSizing = true;
        name.fontSizeMax = 32f;
        name.fontSizeMin = 12f;
        name.overflowMode = TextOverflowModes.Truncate;
        RectTransform nr = name.rectTransform;
        nr.anchorMin = new Vector2(0f, 0.5f);
        nr.anchorMax = new Vector2(1f, 1f);
        nr.offsetMin = new Vector2(textLeft, 0f);
        nr.offsetMax = new Vector2(-(infoSize + 24f), -8f);

        TMP_Text sub = PixelUIKit.CreateText(Font, go.transform, "Sub", SubLine(tierIndex), 22f, TextAlignmentOptions.MidlineLeft, FontStyles.Normal,
                                             new Color(textColor.r, textColor.g, textColor.b, 0.75f));
        sub.raycastTarget = false;
        sub.enableAutoSizing = true;
        sub.fontSizeMax = 22f;
        sub.fontSizeMin = 10f;
        sub.overflowMode = TextOverflowModes.Truncate;
        RectTransform sr = sub.rectTransform;
        sr.anchorMin = new Vector2(0f, 0f);
        sr.anchorMax = new Vector2(1f, 0.5f);
        sr.offsetMin = new Vector2(textLeft, 8f);
        sr.offsetMax = new Vector2(-(infoSize + 24f), 0f);

        int captured = tierIndex;
        RectTransform info = CreateInfoBox(go.transform, infoSize, () => TipFor(captured));
        info.anchorMin = info.anchorMax = info.pivot = new Vector2(1f, 0.5f);
        info.anchoredPosition = new Vector2(-14f, 0f);

        return new Row { rect = rt, picture = pic, tier = tierIndex };
    }

    private string TipFor(int tierIndex)
    {
        PixelClicker.PixelTier t = clicker.Tiers[tierIndex];
        return "<b>" + ShortName(t.displayName) + "</b>\n\n" + PixelGuideTexts.Describe(t.type, t);
    }

    // ------------------------------------------------------------------
    // The 3D pictures
    // ------------------------------------------------------------------

    private Texture StudioTexture(int tier) => PixelTierPicture.Get(clicker, tier, 192);   // (the shared studio of that pixel type)

    /// <summary>Turns and re-renders the pictures of the rows that are in view.</summary>
    private void RenderVisible()
    {
        if (content == null || rows.Count == 0) return;
        RectTransform viewport = content.parent as RectTransform;
        if (viewport == null) return;
        Vector3[] vc = viewCorners, rc = rowCorners;
        viewport.GetWorldCorners(vc);
        float spin = 360f / (spinSeconds > 0.5f ? spinSeconds : 8f);
        foreach (Row row in rows)
        {
            if (row.rect == null) continue;
            row.rect.GetWorldCorners(rc);
            if (rc[2].y < vc[0].y || rc[0].y > vc[2].y) continue;   // scrolled out of view
            PixelTierPicture.Render(row.tier, Time.unscaledTime * spin);
        }
    }

    private static readonly Vector3[] viewCorners = new Vector3[4], rowCorners = new Vector3[4];   // (reused every frame)

    // ------------------------------------------------------------------
    // Per frame
    // ------------------------------------------------------------------

    private void Update()
    {
        if (!built || openObject == null) return;
        bool show = ShowButton();
        if (openObject.activeSelf != show) openObject.SetActive(show);
        if (!show && windowObject.activeSelf) windowObject.SetActive(false);
        if (!windowObject.activeSelf) return;

        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer <= 0f)
        {
            refreshTimer = 0.5f;
            Refresh();
        }
    }

    private void LateUpdate()
    {
        if (!built || windowObject == null) return;
        bool open = windowObject.activeSelf;
        if (open) { FitBesideShop(); RenderVisible(); }
        UpdateTip();
        if (buttonGroup == null) return;
        // The button stays where it is while the window is open, so it can be clicked again to close it (like the Inventory and Log buttons).
        if (dock != null) dock.ForceHidden = false;
        buttonGroup.alpha = 1f;
        buttonGroup.blocksRaycasts = true;
    }

    /// <summary>With the shop open the window is as tall as it and as wide as the Currency panel, on its right (mirrors the Toggles window).</summary>
    private void FitBesideShop()
    {
        RectTransform wr = windowObject.GetComponent<RectTransform>();
        if (shopRef == null) shopRef = PixelFind.First<PixelShop>();
        RectTransform shopRect = shopRef != null ? shopRef.PanelRect : null;
        Canvas canvas = windowObject.GetComponentInParent<Canvas>();
        float scale = canvas != null ? Mathf.Max(0.01f, canvas.rootCanvas.scaleFactor) : 1f;
        PixelHud hud = PixelHud.Instance;
        float inset = hud != null ? hud.SideShownInset : 0f;

        if (shopRect == null || !shopRect.gameObject.activeInHierarchy)
        {
            wr.sizeDelta = windowSize;
            wr.anchoredPosition = new Vector2(-inset, 0f);
            return;
        }
        Vector3[] c = new Vector3[4];
        shopRect.GetWorldCorners(c);
        float shopRight = c[2].x, shopTop = c[2].y, shopBottom = c[0].y;
        float width = shopRef.CurrencyPanelWidth;
        float gap = shopRef.CurrencyPanelGap;
        float rightEdge = (Screen.width - shopRight) / scale - gap - width;
        wr.sizeDelta = new Vector2(width, (shopTop - shopBottom) / scale);
        wr.anchoredPosition = new Vector2(-Mathf.Max(0f, rightEdge), ((shopTop + shopBottom) * 0.5f - Screen.height * 0.5f) / scale);
    }
}
