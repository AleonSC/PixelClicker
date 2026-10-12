using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static PixelInput;

/// <summary>
/// Fancy hover tooltip for blocks: point at the clickable cube or at an old pixel lying around and a card fades in beside the pointer showing
/// a spinning cube in the pixel's colour, its name, a rarity tag, what it pays, a progress bar (hits left on the cube / time left on an old pixel)
/// and the Pixel Guide's description. Switch: Settings > Interface 'Block tooltips' (<see cref="Enabled"/>, per save).
/// </summary>
public class PixelBlockTooltip : MonoBehaviour
{
    private const string PrefEnabled = "PixelClicker.Setting.BlockTips";

    /// <summary>Show the tooltip when hovering a block? Default on.</summary>
    public static bool Enabled
    {
        get => PlayerPrefs.GetInt(PrefEnabled, 1) != 0;
        set { PlayerPrefs.SetInt(PrefEnabled, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    [Header("Block Tooltip")]
    [SerializeField, Tooltip("Turns the tooltip off completely.")]
    private bool disableTooltip;
    [SerializeField, Tooltip("Seconds the pointer must rest on a block before the card appears. 0 = 0.18.")]
    private float hoverDelay;
    [SerializeField, Tooltip("Width of the card in canvas units. 0 = 560.")]
    private float cardWidth;
    [SerializeField, Tooltip("Text size of the description. 0 = 21.")]
    private float descriptionSize;

    private PixelClicker clicker;
    private PixelGrab grab;
    private GameObject canvasObject;
    private Canvas canvas;
    private RectTransform card;
    private CanvasGroup group;
    private Image glow, accent, bar, barFill, shine, iconFrame;
    private PixelCubeIcon icon;
    private TMP_Text nameText, tagText, statsText, descText, barText;
    private RectTransform barRect;

    private int shownTier = -1;
    private bool shownLive;
    private Rigidbody shownBody;
    private float hoverTime;
    private float visible;          // 0..1 fade
    private float nextRefresh;
    private float maxRemaining;     // longest time-left seen for the hovered old pixel (the bar's full length)
    private float shownAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { }

    private void Start()
    {
        clicker = PixelFind.First<PixelClicker>();
        grab = PixelFind.First<PixelGrab>();
    }

    private void OnDestroy()
    {
        if (canvasObject != null) Destroy(canvasObject);
    }

    private static Color Dim(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);

    private void Build()
    {
        float w = cardWidth > 0f ? cardWidth : 560f;
        canvasObject = PixelUIKit.CreateCanvas("Block Tooltip", 655, new Vector2(1920f, 1080f), false);
        canvas = canvasObject.GetComponent<Canvas>();
        TMP_FontAsset font = clicker.UIFont;

        GameObject cardObject = new GameObject("Card", typeof(RectTransform), typeof(CanvasGroup));
        cardObject.transform.SetParent(canvasObject.transform, false);
        card = cardObject.GetComponent<RectTransform>();
        card.anchorMin = card.anchorMax = Vector2.zero;
        card.pivot = new Vector2(0f, 1f);
        card.sizeDelta = new Vector2(w, 200f);
        group = cardObject.GetComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        group.alpha = 0f;

        // Soft pulsing glow in the pixel's colour behind the card.
        GameObject glowObject = new GameObject("Glow", typeof(RectTransform), typeof(Image));
        glowObject.transform.SetParent(card, false);
        glow = glowObject.GetComponent<Image>();
        glow.raycastTarget = false;
        PixelUIKit.StyleBox(glow, true);
        RectTransform gr = glow.rectTransform;
        gr.anchorMin = Vector2.zero; gr.anchorMax = Vector2.one; gr.offsetMin = new Vector2(-9f, -9f); gr.offsetMax = new Vector2(9f, 9f);

        // The window body clips the shine sweep.
        GameObject bodyObject = new GameObject("Body", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        bodyObject.transform.SetParent(card, false);
        Image body = bodyObject.GetComponent<Image>();
        body.raycastTarget = false;
        PixelUIKit.StyleWindow(body, new Color(0.07f, 0.09f, 0.14f, 0.97f));
        PixelUIKit.Stretch(body.rectTransform);

        accent = NewImage(body.transform, "Accent");
        accent.rectTransform.anchorMin = new Vector2(0f, 1f); accent.rectTransform.anchorMax = new Vector2(1f, 1f);
        accent.rectTransform.pivot = new Vector2(0.5f, 1f);
        accent.rectTransform.sizeDelta = new Vector2(0f, 7f);

        iconFrame = NewImage(body.transform, "IconFrame");
        PixelUIKit.StyleBox(iconFrame, true);
        RectTransform fr = iconFrame.rectTransform;
        fr.anchorMin = fr.anchorMax = new Vector2(0f, 1f); fr.pivot = new Vector2(0f, 1f);
        fr.anchoredPosition = new Vector2(20f, -26f); fr.sizeDelta = new Vector2(110f, 110f);

        GameObject iconObject = new GameObject("Cube", typeof(RectTransform), typeof(CanvasRenderer), typeof(PixelCubeIcon));
        iconObject.transform.SetParent(iconFrame.transform, false);
        icon = iconObject.GetComponent<PixelCubeIcon>();
        icon.fill = 0.75f;
        PixelUIKit.Stretch(icon.rectTransform);

        nameText = PixelUIKit.CreateText(font, body.transform, "Name", "", 38f, TextAlignmentOptions.TopLeft, FontStyles.Bold, Color.white);
        PlaceText(nameText, 146f, -22f, w - 166f, 46f);
        nameText.enableAutoSizing = true; nameText.fontSizeMin = 22f; nameText.fontSizeMax = 38f;
        nameText.overflowMode = TextOverflowModes.Ellipsis;

        tagText = PixelUIKit.CreateText(font, body.transform, "Tag", "", 20f, TextAlignmentOptions.TopLeft, FontStyles.Bold, Color.white);
        PlaceText(tagText, 146f, -70f, w - 166f, 28f);

        statsText = PixelUIKit.CreateText(font, body.transform, "Stats", "", 23f, TextAlignmentOptions.TopLeft, FontStyles.Normal, new Color(0.88f, 0.92f, 1f, 1f));
        PlaceText(statsText, 146f, -98f, w - 166f, 60f);

        bar = NewImage(body.transform, "Bar");
        bar.color = new Color(0f, 0f, 0f, 0.55f);
        PixelUIKit.StyleBox(bar, true);
        barRect = bar.rectTransform;
        barRect.anchorMin = barRect.anchorMax = new Vector2(0f, 1f); barRect.pivot = new Vector2(0f, 1f);
        barRect.sizeDelta = new Vector2(w - 40f, 26f);
        barFill = NewImage(bar.transform, "Fill");
        barFill.rectTransform.anchorMin = Vector2.zero; barFill.rectTransform.anchorMax = new Vector2(0f, 1f);
        barFill.rectTransform.offsetMin = new Vector2(3f, 3f); barFill.rectTransform.offsetMax = new Vector2(-3f, -3f);
        barFill.rectTransform.pivot = new Vector2(0f, 0.5f);
        barText = PixelUIKit.CreateText(font, bar.transform, "BarText", "", 17f, TextAlignmentOptions.Center, FontStyles.Bold, Color.white);
        PixelUIKit.Stretch(barText.rectTransform);

        descText = PixelUIKit.CreateText(font, body.transform, "Description", "", descriptionSize > 0f ? descriptionSize : 21f,
                                         TextAlignmentOptions.TopLeft, FontStyles.Normal, new Color(0.78f, 0.84f, 0.95f, 1f));
        descText.richText = true;
        descText.textWrappingMode = TextWrappingModes.Normal;

        // A diagonal sheen that sweeps over the card now and then.
        shine = NewImage(body.transform, "Shine");
        shine.color = new Color(1f, 1f, 1f, 0.10f);
        shine.rectTransform.anchorMin = shine.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        shine.rectTransform.sizeDelta = new Vector2(70f, 900f);
        shine.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 18f);

        canvasObject.SetActive(false);
    }

    private static Image NewImage(Transform parent, string objectName)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private static void PlaceText(TMP_Text t, float x, float y, float width, float height)
    {
        RectTransform r = t.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
        r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = new Vector2(x, y);
        r.sizeDelta = new Vector2(width, height);
    }

    private bool Suppressed()
    {
        return disableTooltip || !Enabled || clicker == null || PixelTitleScreen.Showing || PixelPauseMenu.IsPaused ||
               PixelFirstPerson.Active || PixelRobotWorker.Piloting || PixelClicker.GodMode || PixelCameraIntro.Moving ||
               PointerOverUI() || (grab != null && grab.HeldBody != null) || PixelBank.HoseOn;
    }

    private void Update()
    {
        if (clicker == null) { clicker = PixelFind.First<PixelClicker>(); return; }
        if (grab == null) grab = PixelFind.First<PixelGrab>();

        int tier = -1; bool live = false; Rigidbody body = null;
        bool hovering = !Suppressed() && clicker.TryGetBlockAt(PointerPosition(), out tier, out live, out body);

        bool same = hovering && tier == shownTier && live == shownLive && body == shownBody;
        if (hovering && !same)
        {
            shownTier = tier; shownLive = live; shownBody = body;
            hoverTime = 0f; maxRemaining = 0f; nextRefresh = 0f;
        }
        else if (hovering) hoverTime += Time.unscaledDeltaTime;
        else hoverTime = 0f;

        float delay = hoverDelay > 0f ? hoverDelay : 0.18f;
        bool want = hovering && hoverTime >= delay;
        float before = visible;
        visible = Mathf.MoveTowards(visible, want ? 1f : 0f, Time.unscaledDeltaTime * (want ? 7f : 10f));
        if (visible <= 0f)
        {
            if (canvasObject != null && canvasObject.activeSelf) canvasObject.SetActive(false);
            if (!hovering) { shownTier = -1; shownBody = null; }
            return;
        }
        if (canvasObject == null) Build();
        if (!canvasObject.activeSelf) { canvasObject.SetActive(true); shownAt = Time.unscaledTime; nextRefresh = 0f; }
        if (before <= 0f) shownAt = Time.unscaledTime;

        if (Time.unscaledTime >= nextRefresh && shownTier >= 0)
        {
            nextRefresh = Time.unscaledTime + 0.25f;
            Refresh();
        }
        Animate();
        Follow();
    }

    private static string RarityOf(PixelClicker.PixelTier t, float share, out Color color)
    {
        if (t.rareDrop) { color = new Color(1f, 0.55f, 0.15f); return "MYTHIC"; }
        if (share <= 0f) { color = new Color(0.65f, 0.7f, 0.8f); return "SPECIAL"; }
        if (share >= 0.20f) { color = new Color(0.78f, 0.82f, 0.88f); return "COMMON"; }
        if (share >= 0.08f) { color = new Color(0.45f, 0.9f, 0.5f); return "UNCOMMON"; }
        if (share >= 0.03f) { color = new Color(0.4f, 0.7f, 1f); return "RARE"; }
        if (share >= 0.01f) { color = new Color(0.8f, 0.5f, 1f); return "EPIC"; }
        color = new Color(1f, 0.82f, 0.25f);
        return "LEGENDARY";
    }

    private static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

    private void Refresh()
    {
        PixelClicker.PixelTier[] tiers = clicker.Tiers;
        if (shownTier < 0 || shownTier >= tiers.Length) return;
        PixelClicker.PixelTier t = tiers[shownTier];
        Color c = t.UIColor;
        string pretty = t.displayName.EndsWith(" Pixels") ? t.displayName.Substring(0, t.displayName.Length - 7) : t.displayName;
        Color rarityColor;
        string rarity = RarityOf(t, clicker.SpawnShare(shownTier), out rarityColor);

        accent.color = c;
        glow.color = new Color(c.r, c.g, c.b, 0.35f);
        iconFrame.color = new Color(c.r * 0.25f, c.g * 0.25f, c.b * 0.25f, 1f);
        icon.color = c;
        nameText.text = pretty;
        nameText.color = Color.Lerp(c, Color.white, 0.25f);

        string where = shownLive ? "ON THE CUBE" : "OLD PIXEL";
        tagText.text = "<color=#" + Hex(rarityColor) + ">" + rarity + "</color>  <color=#8f9bb5>-  " + where + "</color>";

        double amount;
        string pays;
        if (shownLive)
        {
            amount = t.amountPerClick * clicker.PayoutMultiplier(shownTier);
            pays = t.randomPayout ? t.payoutMin + " - " + t.payoutMax : PixelClicker.FormatNumber(amount);
        }
        else
        {
            OldPixelInfo info = shownBody != null ? shownBody.GetComponent<OldPixelInfo>() : null;
            amount = info != null ? info.amount : 0.0;
            pays = PixelClicker.FormatNumber(amount);
        }
        string stats = "Worth <b><color=#" + Hex(Color.Lerp(c, Color.white, 0.4f)) + ">" + pays + "</color></b>";
        if (!shownLive) stats += "  when collected";

        float frac = 0f; string barLabel = ""; Color barColor = c; bool showBar = false;
        if (shownLive)
        {
            clicker.GetLiveProgress(out int hits, out int needed);
            if (needed > 1)
            {
                showBar = true;
                frac = Mathf.Clamp01(hits / (float)needed);
                int left = Mathf.Max(1, needed - hits);
                barLabel = left + (left == 1 ? " click left" : " clicks left");
                stats += "\nClicks left: <b>" + left + "</b> of " + needed;
            }
            else stats += "\nClicks left: <b>1</b>";
        }
        else if (shownBody != null)
        {
            OldPixelDespawn d = shownBody.GetComponent<OldPixelDespawn>();
            float rem = d != null ? d.Remaining : -1f;
            if (rem >= 0f)
            {
                showBar = true;
                if (rem > maxRemaining) maxRemaining = rem;
                frac = maxRemaining > 0.01f ? Mathf.Clamp01(rem / maxRemaining) : 0f;
                barLabel = "Vanishes in " + Mathf.CeilToInt(rem) + "s";
                barColor = Color.Lerp(new Color(1f, 0.35f, 0.25f), c, frac);
            }
            else stats += "\nIt stays until it is collected";
        }
        statsText.text = stats;

        string desc = shownLive ? PixelGuideTexts.Describe(t.type, t) : "<b>Old pixel:</b> " + PixelGuideTexts.OldPixel(t.type);
        descText.text = desc;

        float w = card.sizeDelta.x;
        float y = 98f + statsText.GetPreferredValues(statsText.text, w - 166f, 0f).y;
        y = Mathf.Max(y, 26f + 110f) + 14f;
        bar.gameObject.SetActive(showBar);
        if (showBar)
        {
            barRect.anchoredPosition = new Vector2(20f, -y);
            barFill.color = barColor;
            barFill.rectTransform.anchorMax = new Vector2(Mathf.Max(0.001f, frac), 1f);
            barText.text = barLabel;
            y += 26f + 14f;
        }
        float descHeight = descText.GetPreferredValues(desc, w - 40f, 0f).y;
        PlaceText(descText, 20f, -y, w - 40f, descHeight);
        card.sizeDelta = new Vector2(w, y + descHeight + 20f);
    }

    private void Animate()
    {
        float t = Time.unscaledTime;
        float ease = 1f - Mathf.Pow(1f - visible, 3f);
        group.alpha = ease;
        card.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, ease);
        Color g = glow.color;
        g.a = (0.25f + 0.15f * Mathf.Sin(t * 3f)) * ease;
        glow.color = g;
        // The sheen sweeps across for about 0.7 s every 3 seconds.
        float phase = Mathf.Repeat(t - shownAt, 3f) / 0.7f;
        float w = card.sizeDelta.x;
        shine.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-120f, w + 120f, Mathf.Clamp01(phase)), 0f);
        shine.enabled = phase < 1f;
    }

    private void Follow()
    {
        float scaleFactor = canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
        Vector2 p = PointerPosition() / scaleFactor;
        Vector2 size = card.sizeDelta;
        Vector2 canvasSize = new Vector2(Screen.width, Screen.height) / scaleFactor;
        float bar = PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f;
        // To the lower right of the pointer; flipped to the other side when it would leave the screen.
        float x = p.x + 28f;
        if (x + size.x > canvasSize.x - 8f) x = p.x - 28f - size.x;
        float y = p.y - 18f;
        if (y - size.y < bar + 8f) y = p.y + 24f + size.y;
        x = Mathf.Clamp(x, 8f, Mathf.Max(8f, canvasSize.x - size.x - 8f));
        y = Mathf.Clamp(y, bar + size.y + 8f, Mathf.Max(bar + size.y + 8f, canvasSize.y - bar - 8f));
        card.anchoredPosition = new Vector2(x, y);
    }
}
