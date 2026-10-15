// ---- System notes (moved here from CLAUDE.md) ----
// PixelBlockTooltip.cs
//   Block hover tooltip (added by PixelClicker.Awake; Settings > Interface 'Block tooltips', PixelBlockTooltip.Enabled, PlayerPrefs
//   PixelClicker.Setting.BlockTips, default on, per save; disableTooltip, hoverDelay 0 = 0.18, cardWidth 0 = 560):
//   PixelClicker.TryGetBlockAt(screen, ...) finds the nearest of the clickable cube / an old pixel under the pointer; after the hover
//   delay a card (canvas 655, glow + fancy window + accent strip in the pixel colour, real turning 3D model (PixelTierPicture), name, rarity tag from
//   SpawnShare, worth, a bar for hits (GetLiveProgress) or old-pixel time left, PixelGuideTexts.Describe / OldPixel text, a sheen
//   sweep) fades in beside the pointer. Hidden over UI, paused, title, first person, UFO cockpit, hose or god mode. THREE MODES (PixelBlockTooltip.Mode, Settings > Interface drop-down, PlayerPrefs BlockTipsMode, per save): 0 Full, 1 Minimal (the spinning 3D icon and the name only), 2 Off. ALT: a tap (released within altHoldSeconds 0.25) cycles Full -> Minimal -> Off -> Full and a small toast names the new mode; HOLDING Alt (longer than that) shows the other kind of the current mode while held (Full shows Minimal, Minimal shows Full, Off shows Full) and the release does not cycle. Grabbing a block keeps its tooltip open. The card keeps its spot until the pointer moves 70 units away from where it was placed, then glides there (so it no longer jitters with every mouse move).
//   PINNED: My Window's Info button (PixelMyWindow.HoverDocked) moves the card into the window (UpdateDocked / Undock): it stops following the mouse, is scaled to the window's width, shows whatever is hovered (it keeps the last block while the mouse is over the window) and the window grows to fit it.
// ---------------------------------------------------
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

    private const string PrefMode = "PixelClicker.Setting.BlockTipsMode";
    private const int ModeFull = 0, ModeMinimal = 1, ModeOff = 2;
    public static readonly string[] ModeNames = { "Full", "Minimal", "Off" };

    /// <summary>0 = full card, 1 = minimal (3D icon + name), 2 = off. Old saves: the former on / off tick box (BlockTips) maps to Full / Off.</summary>
    public static int Mode
    {
        get
        {
            if (PlayerPrefs.HasKey(PrefMode)) return Mathf.Clamp(PlayerPrefs.GetInt(PrefMode, 0), 0, 2);
            return PlayerPrefs.GetInt(PrefEnabled, 1) != 0 ? ModeFull : ModeOff;
        }
        set { PlayerPrefs.SetInt(PrefMode, Mathf.Clamp(value, 0, 2)); PlayerPrefs.Save(); }
    }

    /// <summary>Show the tooltip when hovering a block at all? (Mode is not Off.)</summary>
    public static bool Enabled
    {
        get => Mode != ModeOff;
        set { if (value && Mode == ModeOff) Mode = ModeFull; else if (!value) Mode = ModeOff; }
    }

    private static bool AltHeld() => PixelKeys.KeyHeld(KeyCode.LeftAlt) || PixelKeys.KeyHeld(KeyCode.RightAlt);

    [Header("Block Tooltip")]
    [SerializeField, Tooltip("Turns the tooltip off completely.")]
    private bool disableTooltip;
    [SerializeField, Tooltip("Seconds the pointer must rest on a block before the card appears. 0 = 0.18.")]
    private float hoverDelay;
    [SerializeField, Tooltip("Width of the card in canvas units. 0 = 560.")]
    private float cardWidth;
    [SerializeField, Tooltip("Text size of the description. 0 = 21.")]
    private float descriptionSize;
    [SerializeField, Tooltip("Seconds Alt must be held before it counts as 'peek the other kind' (a shorter press cycles the mode instead). 0 = 0.25.")]
    private float altHoldSeconds;

    private PixelClicker clicker;
    private PixelGrab grab;
    private GameObject canvasObject;
    private Canvas canvas;
    private RectTransform card;
    private CanvasGroup group;
    private Image glow, accent, bar, barFill, shine, iconFrame;
    private PixelCubeIcon icon;
    private RawImage picture;      // the block's REAL 3D model (PixelTierPicture); the flat cube icon is only the fallback
    private TMP_Text nameText, tagText, statsText, descText, barText;
    private RectTransform barRect;

    private int shownTier = -1;
    private bool shownLive;
    private Rigidbody shownBody;
    private float hoverTime;
    private float visible;          // 0..1 fade
    private float nextRefresh;
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

        GameObject pictureObject = new GameObject("Model", typeof(RectTransform), typeof(RawImage));
        pictureObject.transform.SetParent(iconFrame.transform, false);
        picture = pictureObject.GetComponent<RawImage>();
        picture.raycastTarget = false;
        PixelUIKit.Stretch(picture.rectTransform);
        picture.rectTransform.offsetMin = new Vector2(4f, 4f); picture.rectTransform.offsetMax = new Vector2(-4f, -4f);
        picture.gameObject.SetActive(false);

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

        toast = PixelUIKit.CreateText(font, canvasObject.transform, "Mode Toast", "", 30f, TextAlignmentOptions.Left, FontStyles.Bold, Color.white);
        toast.raycastTarget = false;
        toast.rectTransform.anchorMin = toast.rectTransform.anchorMax = Vector2.zero;
        toast.rectTransform.pivot = new Vector2(0f, 0f);
        toast.rectTransform.sizeDelta = new Vector2(420f, 40f);
        toast.gameObject.SetActive(false);

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

    // ---- Alt: a tap cycles the mode, holding peeks at the other kind ----
    private bool altWas, altPeek;
    private int lastMode = -1;
    private float altDownAt, toastUntil;
    private string toastText = "";
    private TMP_Text toast;

    /// <summary>The kind of card shown right now: the saved mode, or - while Alt is held long enough - the other kind (Full shows Minimal, Minimal and Off show Full).</summary>
    private int EffectiveMode
    {
        get
        {
            int m = Mode;
            if (!altPeek) return m;
            return m == ModeFull ? ModeMinimal : ModeFull;
        }
    }

    private void UpdateAlt()
    {
        bool alt = !(PixelCalm.InMenu) && AltHeld();
        if (alt && !altWas) { altDownAt = Time.unscaledTime; altPeek = false; }
        if (alt && !altPeek && Time.unscaledTime - altDownAt >= (altHoldSeconds > 0f ? altHoldSeconds : 0.25f)) altPeek = true;
        if (!alt && altWas)
        {
            if (!altPeek)
            {
                Mode = (Mode + 1) % 3;   // a tap: Full -> Minimal -> Off -> Full
                toastText = "Tooltips: " + ModeNames[Mode];
                toastUntil = Time.unscaledTime + 1.3f;
                shownTier = -1; shownBody = null; hoverTime = 0f;   // (the card re-forms in the new kind)
            }
            altPeek = false;
        }
        altWas = alt;
    }

    private bool Suppressed(bool whileHeld = false)
    {
        return disableTooltip || EffectiveMode == ModeOff || clicker == null || PixelCalm.InMenu ||
               PixelFirstPerson.Active || PixelRobotWorker.Piloting || PixelClicker.GodMode || PixelCameraIntro.Moving ||
               (!whileHeld && (PointerOverUI() || (grab != null && grab.HeldBody != null))) || PixelBank.HoseOn;
    }

    private void Update()
    {
        if (clicker == null) { clicker = PixelFind.First<PixelClicker>(); return; }
        if (grab == null) grab = PixelFind.First<PixelGrab>();
        UpdateAlt();
        if (EffectiveMode != lastMode) { lastMode = EffectiveMode; nextRefresh = 0f; }   // (peeking at the other kind re-lays the card at once)

        int tier = -1; bool live = false; Rigidbody body = null;
        bool hovering;
        Rigidbody heldBody = grab != null ? grab.HeldBody : null;
        OldPixelInfo heldInfo = heldBody != null ? heldBody.GetComponent<OldPixelInfo>() : null;
        if (heldInfo != null && clicker.IsValidTierIndex(heldInfo.tierIndex) && !Suppressed(true))
        {
            // Grabbing a block keeps its tooltip open (it follows the block you carry).
            tier = heldInfo.tierIndex; live = false; body = heldBody; hovering = true;
        }
        else hovering = !Suppressed() && clicker.TryGetBlockAt(PointerPosition(), out tier, out live, out body);

        // Pinned to My Window (its Info button): the card lives in the window instead of following the mouse, and keeps the last block shown while the mouse is over the window.
        if (PixelMyWindow.TryGetHoverSlot(out RectTransform slot, out float slotWidth) && !disableTooltip && EffectiveMode != ModeOff && !PixelCalm.InMenu)
        {
            UpdateDocked(slot, slotWidth, hovering, tier, live, body);
            return;
        }
        if (wasDocked) Undock();

        bool same = hovering && tier == shownTier && live == shownLive && body == shownBody;
        if (hovering && !same)
        {
            shownTier = tier; shownLive = live; shownBody = body;
            hoverTime = 0f; nextRefresh = 0f;
        }
        else if (hovering) hoverTime += Time.unscaledDeltaTime;
        else hoverTime = 0f;

        float delay = hoverDelay > 0f ? hoverDelay : 0.18f;
        bool want = hovering && hoverTime >= delay;
        float before = visible;
        visible = Mathf.MoveTowards(visible, want ? 1f : 0f, Time.unscaledDeltaTime * (want ? 7f : 10f));
        if (visible <= 0f && Time.unscaledTime < toastUntil)
        {
            if (canvasObject == null) Build();
            if (!canvasObject.activeSelf) canvasObject.SetActive(true);
            group.alpha = 0f;
            UpdateToast();
            return;
        }
        if (visible <= 0f)
        {
            if (canvasObject != null && canvasObject.activeSelf) canvasObject.SetActive(false);
            if (!hovering) { shownTier = -1; shownBody = null; }
            anchored = false;
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
        UpdateToast();
    }

    // ---- Pinned to My Window ----
    private bool wasDocked;

    private void UpdateDocked(RectTransform slot, float slotWidth, bool hovering, int tier, bool live, Rigidbody body)
    {
        if (canvasObject == null) Build();
        if (hovering && (tier != shownTier || live != shownLive || body != shownBody))
        {
            shownTier = tier; shownLive = live; shownBody = body;
            nextRefresh = 0f; shownAt = Time.unscaledTime;
        }
        if (!wasDocked || card.parent != slot)
        {
            wasDocked = true;
            card.SetParent(slot, false);
            card.anchorMin = card.anchorMax = new Vector2(0f, 1f);
            card.pivot = new Vector2(0f, 1f);
            card.anchoredPosition = Vector2.zero;
            nextRefresh = 0f; shownAt = Time.unscaledTime;
        }
        if (canvasObject.activeSelf) canvasObject.SetActive(false);   // (the floating canvas is not needed: the card lives in the window)
        bool have = shownTier >= 0;
        visible = have ? 1f : 0f;
        group.alpha = have ? 1f : 0f;
        if (!have) { PixelMyWindow.SetHoverHeight(0f); return; }
        if (Time.unscaledTime >= nextRefresh) { nextRefresh = Time.unscaledTime + 0.25f; Refresh(); }
        Animate();
        float k = Mathf.Min(1f, slotWidth / Mathf.Max(1f, card.sizeDelta.x));
        card.localScale = Vector3.one * k;
        PixelMyWindow.SetHoverHeight(card.sizeDelta.y * k);
    }

    /// <summary>Back to the normal card that follows the mouse.</summary>
    private void Undock()
    {
        wasDocked = false;
        if (card != null && canvasObject != null)
        {
            card.SetParent(canvasObject.transform, false);
            card.anchorMin = card.anchorMax = Vector2.zero;
            card.pivot = new Vector2(0f, 1f);
            card.localScale = Vector3.one;
            group.alpha = 0f;
        }
        visible = 0f; shownTier = -1; shownBody = null; hoverTime = 0f; anchored = false;
        PixelMyWindow.SetHoverHeight(-1f);
    }

    /// <summary>The little 'Tooltips: Minimal' note beside the pointer after an Alt tap.</summary>
    private void UpdateToast()
    {
        if (toast == null) return;
        float left = toastUntil - Time.unscaledTime;
        bool on = left > 0f;
        if (toast.gameObject.activeSelf != on) toast.gameObject.SetActive(on);
        if (!on) return;
        toast.text = toastText;
        toast.color = new Color(1f, 0.9f, 0.5f, Mathf.Clamp01(left / 0.4f));
        float scaleFactor = canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
        toast.rectTransform.anchoredPosition = PointerPosition() / scaleFactor + new Vector2(30f, 36f);
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
        Texture model = PixelTierPicture.Get(clicker, shownTier, 192);   // the real model, turning (null if the block has none: then the flat icon shows)
        picture.texture = model;
        picture.gameObject.SetActive(model != null);
        icon.gameObject.SetActive(model == null);
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
            if (d != null && d.IsDespawning)
            {
                showBar = true;
                frac = 0.04f;
                barLabel = "Vanishing...";
                barColor = new Color(1f, 0.35f, 0.25f);
            }
            else if (d != null && d.Remaining >= 0f)
            {
                showBar = true;
                frac = Mathf.Clamp01(1f - d.AgeFraction);   // how much of its whole lifespan is left (not 'of the longest time seen while hovering')
                float rem = d.RemainingReal;
                int whole = Mathf.CeilToInt(rem);
                string time = whole >= 60 ? (whole / 60) + "m " + (whole % 60) + "s" : whole + "s";
                barLabel = "Vanishes in " + time + (d.Paused ? " (paused)" : "");
                barColor = Color.Lerp(new Color(1f, 0.35f, 0.25f), c, frac);
            }
            else stats += "\nIt stays until it is collected";
        }
        statsText.text = stats;

        bool mini = EffectiveMode == ModeMinimal;
        tagText.gameObject.SetActive(!mini);
        statsText.gameObject.SetActive(!mini);
        descText.gameObject.SetActive(!mini);
        RectTransform fr = iconFrame.rectTransform;
        if (mini)
        {
            // Minimal: the spinning 3D icon and the name, nothing else.
            bar.gameObject.SetActive(false);
            fr.anchoredPosition = new Vector2(20f, -20f); fr.sizeDelta = new Vector2(100f, 100f);
            float nameWidth = nameText.GetPreferredValues(pretty, 600f, 0f).x;
            float mw = Mathf.Clamp(140f + nameWidth + 24f, 280f, 600f);
            PlaceText(nameText, 140f, -36f, mw - 160f, 46f);
            card.sizeDelta = new Vector2(mw, 140f);
            return;
        }
        fr.anchoredPosition = new Vector2(20f, -26f); fr.sizeDelta = new Vector2(110f, 110f);
        PlaceText(nameText, 146f, -22f, (cardWidth > 0f ? cardWidth : 560f) - 166f, 46f);

        string desc = shownLive ? PixelGuideTexts.Describe(t.type, t) : "<b>Old pixel:</b> " + PixelGuideTexts.OldPixel(t.type);
        descText.text = desc;

        float w = cardWidth > 0f ? cardWidth : 560f;
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
        if (shownTier >= 0 && picture != null && picture.gameObject.activeSelf) PixelTierPicture.Render(shownTier, t * 55f);
        float ease = 1f - Mathf.Pow(1f - visible, 3f);
        group.alpha = ease;
        if (!wasDocked) card.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, ease);   // (pinned in My Window the scale fits the window instead)
        Color g = glow.color;
        g.a = (0.25f + 0.15f * Mathf.Sin(t * 3f)) * ease;
        glow.color = g;
        // The sheen sweeps across for about 0.7 s every 3 seconds.
        float phase = Mathf.Repeat(t - shownAt, 3f) / 0.7f;
        float w = card.sizeDelta.x;
        shine.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-120f, w + 120f, Mathf.Clamp01(phase)), 0f);
        shine.enabled = phase < 1f;
    }

    private Vector2 anchorPoint;          // where the pointer was when the card last picked a spot (the card only re-places itself when the pointer moves well away from it)
    private bool anchored;
    private Vector2 smoothPos;
    private const float DeadZone = 70f;   // canvas units the pointer may wander before the card moves

    private void Follow()
    {
        float scaleFactor = canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
        Vector2 pointer = PointerPosition() / scaleFactor;
        if (!anchored || (pointer - anchorPoint).sqrMagnitude > DeadZone * DeadZone) { anchorPoint = pointer; if (!anchored) { anchored = true; smoothPos = Vector2.zero; } }
        Vector2 p = anchorPoint;
        Vector2 size = card.sizeDelta;
        Vector2 canvasSize = new Vector2(Screen.width, Screen.height) / scaleFactor;
        float bar = PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f;
        // To the lower LEFT of the anchor; flipped to the lower right when it would leave the screen.
        float x = p.x - 40f - size.x;
        if (x < 8f) x = p.x + 40f;
        float y = p.y - 24f;
        if (y - size.y < bar + 8f) y = p.y + 30f + size.y;
        x = Mathf.Clamp(x, 8f, Mathf.Max(8f, canvasSize.x - size.x - 8f));
        y = Mathf.Clamp(y, bar + size.y + 8f, Mathf.Max(bar + size.y + 8f, canvasSize.y - bar - 8f));
        Vector2 target = new Vector2(x, y);
        // Glide to the spot (so the little re-placements are not jumps).
        if (smoothPos == Vector2.zero || (smoothPos - target).sqrMagnitude > 600f * 600f) smoothPos = target;
        smoothPos = Vector2.Lerp(smoothPos, target, 1f - Mathf.Exp(-9f * Time.unscaledDeltaTime));
        card.anchoredPosition = smoothPos;
    }
}
