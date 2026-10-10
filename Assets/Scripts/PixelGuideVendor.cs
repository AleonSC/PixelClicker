using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cubie, the suspicious cube shopkeeper who acts as the game's first guide (added by <see cref="PixelClicker"/>.Awake).
/// Visit 1 (new game, once the intro tips are done): he slides in from the side, says he'll show you his shop if you can prove you have
/// money, and leaves; you go and click pixels. Visit 2 (when the Shop button appears): he introduces the shop, gives you a "gift" (a random
/// assortment of pixels), says more vendors will come as you progress and can be switched off in the Toggles menu, and mentions his friend
/// the Wizard. The vendors (Wizard, Tinkerer, Pixel Entrepreneur) are no longer bought in the shop: this script starts them (the Wizard soon
/// after visit 2, the others when enough pixel types are unlocked). Progress is stored in the save file (stat counters guide.stage).
/// Saves that already have the shop get no visit and no gift; their vendors just start.
/// </summary>
public class PixelGuideVendor : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The PixelClicker. Found automatically if left empty.")]
    [SerializeField] private PixelClicker clicker;

    [Tooltip("The shop (its Shop button appearing is the moment of visit 2). Found automatically if left empty.")]
    [SerializeField] private PixelShop shop;

    [Header("General")]
    [Tooltip("Turn the guide (both visits and the free vendors) off.")]
    [SerializeField] private bool disableGuide = false;

    [Min(0f)]
    [Tooltip("Seconds of calm (title screen over, no tip box, no minigame round, not paused) before a visit starts.")]
    [SerializeField] private float calmSeconds = 1.5f;

    [Min(0.1f)]
    [Tooltip("Seconds he takes to slide in (and out).")]
    [SerializeField] private float slideSeconds = 1.3f;

    [Min(100f)]
    [Tooltip("Height of the character (canvas units).")]
    [SerializeField] private float characterHeight = 400f;

    [Header("Texts")]
    [Tooltip("Name shown at the top of his speech box.")]
    [SerializeField] private string characterName = "Cubie";

    [TextArea(2, 4)]
    [Tooltip("Visit 1, one entry per page. Empty = the built-in lines.")]
    [SerializeField] private string[] visitOnePages = new string[0];

    [TextArea(2, 4)]
    [Tooltip("Visit 2, one entry per page. The page that contains {gift} shows the gift and is when it is handed over. Empty = the built-in lines.")]
    [SerializeField] private string[] visitTwoPages = new string[0];

    [Tooltip("Button label for the next page.")]
    [SerializeField] private string nextText = "Next";

    [Tooltip("Button label on the last page.")]
    [SerializeField] private string lastText = "Okay!";

    [Header("Gift")]
    [Min(1)]
    [Tooltip("How many different pixel types are in the gift pack.")]
    [SerializeField] private int giftTypes = 3;

    [Min(1)]
    [Tooltip("Fewest pixels of one type in the gift.")]
    [SerializeField] private int giftMin = 40;

    [Min(1)]
    [Tooltip("Most pixels of one type in the gift.")]
    [SerializeField] private int giftMax = 120;

    [Header("Free vendors")]
    [Min(1f)]
    [Tooltip("The Wizard's first visit comes this many seconds (at the earliest) after visit 2.")]
    [SerializeField] private float wizardFirstVisitMin = 60f;

    [Min(1f)]
    [Tooltip("The Wizard's first visit comes this many seconds (at the latest) after visit 2.")]
    [SerializeField] private float wizardFirstVisitMax = 100f;

    [Min(1)]
    [Tooltip("The Tinkerer starts visiting once this many pixel types are unlocked (6 = after the RGB pack).")]
    [SerializeField] private int tinkererAtTiers = 6;

    [Min(1)]
    [Tooltip("The Pixel Entrepreneur starts visiting once this many pixel types are unlocked.")]
    [SerializeField] private int entrepreneurAtTiers = 8;

    [Min(1)]
    [Tooltip("The Farmer (sells seeds) starts visiting once this many pixel types are unlocked.")]
    [SerializeField] private int farmerAtTiers = 5;

    [Min(1f)]
    [Tooltip("Seconds until a newly started Tinkerer / Entrepreneur makes their first visit (random between this and twice this).")]
    [SerializeField] private float otherFirstVisitSeconds = 60f;

    private static PixelGuideVendor instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; }

    private const string Stage1Key = "guide.stage1";
    private const string Stage2Key = "guide.stage2";

    /// <summary>True while the guide still has to show visit 2 (the shop tutorial waits for it).</summary>
    public static bool Pending => instance != null && !instance.disableGuide && PixelStats.Total(Stage2Key) < 1d;

    /// <summary>Settings > Replay tutorial: Cubie's two visits play again (without a second gift).</summary>
    public static void Replay()
    {
        if (instance == null) return;
        PixelStats.Clear(Stage1Key);
        PixelStats.Clear(Stage2Key);
        instance.replaying = true;
        instance.calmTimer = 0f;
    }

    /// <summary>True while he is on screen.</summary>
    public static bool Visiting => instance != null && instance.visiting;

    private PixelStats stats;
    private bool replaying; // Settings > Replay tutorial: both visits again, no second gift
    private bool visiting;
    private float calmTimer, vendorTimer;
    private float savedTimeScale = 1f;
    private bool froze;
    private GameObject canvasRoot;
    private RectTransform characterRect, panel, giftBox, pupilL, pupilR;
    private TMP_Text message;
    private Button nextButton;
    private TMP_Text nextLabel;
    private int advance; // incremented by the button / Escape
    private TMP_FontAsset Font => clicker != null ? clicker.UIFont : null;

    private void Awake()
    {
        instance = this;
        if (characterName == "Cubby") characterName = "Cubie"; // a scene saved before the rename
        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
        if (shop == null) shop = PixelFind.First<PixelShop>();
    }

    private void OnDestroy()
    {
        Unfreeze();
        PixelWindows.Unregister(this);
        if (instance == this) instance = null;
        if (canvasRoot != null) Destroy(canvasRoot);
    }

    private bool Calm()
    {
        if (PixelTitleScreen.Showing || PixelHints.IntroHoldsGuide || PixelNotice.IsShowing || PixelPauseMenu.IsPaused ||
            PixelMinigame.TakeoverActive || PixelPets.PopupOpen || PixelCameraIntro.ClicksLocked || PixelCameraIntro.Moving ||
            Time.timeScale <= 0f) return false;
        foreach (PixelMinigame m in PixelMinigame.All) if (m != null && m.Busy) return false;
        return true;
    }

    private bool ShopThere => shop != null && shop.ShopAvailable;

    private void Update()
    {
        if (disableGuide || clicker == null || visiting) return;
        if (shop == null) shop = PixelFind.First<PixelShop>();
        if (stats == null) stats = PixelFind.First<PixelStats>();
        if (stats == null) return; // progress is kept in the stat counters, so nothing happens without them

        if (!Calm()) { calmTimer = 0f; return; }
        calmTimer += Time.unscaledDeltaTime;
        if (calmTimer < calmSeconds) return;

        bool s1 = PixelStats.Total(Stage1Key) >= 1d, s2 = PixelStats.Total(Stage2Key) >= 1d;
        if (!s2)
        {
            if (ShopThere && !s1 && !replaying)
            {
                // A save that already reached the shop: no visits, no gift - just the vendors from now on.
                PixelStats.Best(Stage1Key, 1d);
                PixelStats.Best(Stage2Key, 1d);
                StartVendors(true);
            }
            else if (ShopThere) StartCoroutine(Visit(true));
            else if (!s1) StartCoroutine(Visit(false));
            return;
        }

        vendorTimer -= Time.unscaledDeltaTime;
        if (vendorTimer <= 0f)
        {
            vendorTimer = 3f;
            StartVendors(false);
        }
    }

    // ------------------------------------------------------------------
    // The free vendors
    // ------------------------------------------------------------------

    private int UnlockedTypes()
    {
        int n = 0;
        foreach (PixelClicker.PixelTier t in clicker.Tiers) if (t.unlocked && !t.rareDrop && !PixelClicker.IsStandalone(t.type)) n++;
        return n;
    }

    /// <summary>Starts every vendor whose milestone is reached (once; a switched-off vendor stays off). 'catchUp' = an old save: the Wizard doesn't get the 'soon' visit.</summary>
    private void StartVendors(bool catchUp)
    {
        TryStart("wizard", true, Random.Range(wizardFirstVisitMin, wizardFirstVisitMax));
        TryStart("tinkerer", UnlockedTypes() >= tinkererAtTiers, Random.Range(otherFirstVisitSeconds, otherFirstVisitSeconds * 2f));
        TryStart("farmer", UnlockedTypes() >= farmerAtTiers, Random.Range(otherFirstVisitSeconds, otherFirstVisitSeconds * 2f));
        TryStart("entrepreneur", UnlockedTypes() >= entrepreneurAtTiers, Random.Range(otherFirstVisitSeconds, otherFirstVisitSeconds * 2f));
    }

    private static void TryStart(string id, bool condition, float firstVisit)
    {
        if (!condition) return;
        PixelMinigame m = PixelMinigame.Find(id);
        if (m == null || m.Running || m.UserDisabled) return;
        m.Activate();
        if (m is PixelVisitorMinigame v) v.ScheduleFirstVisit(firstVisit);
    }

    // ------------------------------------------------------------------
    // A visit
    // ------------------------------------------------------------------

    private string[] DefaultOne => new[]
    {
        "Psst... over here. The name's Cubie. I run a little shop. A very legitimate little shop.",
        "But I only let customers in if they can prove they've got money. So go on: click those pixels and collect some. Come and find me when you're loaded.",
    };

    private string[] DefaultTwo => new[]
    {
        "Well, well... you actually did it. You've got money! Welcome to my shop. You'll find the Shop button at the top right of the screen. Don't worry, I'm not watching your pockets. Much.",
        "Here, a welcome gift. Nothing suspicious about it, I promise.\n\n{gift}",
        "A word of advice: more vendors will pass through as you progress. If you'd rather not be bothered, you can switch any of them off in the Toggles menu (it opens up once you get the Auto Clicker).",
        "Oh, and my friend the Wizard might be coming by soon. Don't let him near my stock, he turns things into frogs. Be seeing you...",
    };

    private IEnumerator Visit(bool second)
    {
        visiting = true;
        advance = 0;
        string[] pages = second ? (visitTwoPages != null && visitTwoPages.Length > 0 ? visitTwoPages : DefaultTwo)
                                : (visitOnePages != null && visitOnePages.Length > 0 ? visitOnePages : DefaultOne);
        BuildUI();

        float barH = PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f;
        float w = characterRect.sizeDelta.x;
        float restX = -(w * 0.5f + 50f);
        float hiddenX = w * 0.5f + 40f;
        characterRect.anchoredPosition = new Vector2(hiddenX, barH + 24f);

        for (float t = 0f; t < slideSeconds; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / slideSeconds);
            characterRect.anchoredPosition = new Vector2(Mathf.Lerp(hiddenX, restX, k), barH + 24f + Mathf.Abs(Mathf.Sin(t * 9f)) * 10f);
            Glance();
            yield return null;
        }
        characterRect.anchoredPosition = new Vector2(restX, barH + 24f);

        Freeze();
        PixelWindows.Register(this, 150, () => true, () => advance++); // Escape = next page
        panel.gameObject.SetActive(true);

        for (int i = 0; i < pages.Length; i++)
        {
            string text = pages[i];
            if (text.Contains("{gift}"))
            {
                text = text.Replace("{gift}", replaying ? "(You already got yours, no seconds!)" : GiveGift());
                if (giftBox != null) giftBox.gameObject.SetActive(true);
            }
            PixelUIKit.SetText(message, text);
            PixelUIKit.SetText(nextLabel, i == pages.Length - 1 ? lastText : nextText);
            int start = advance;
            while (advance == start)
            {
                characterRect.anchoredPosition = new Vector2(restX, barH + 24f + Mathf.Sin(Time.unscaledTime * 3f) * 4f);
                Glance();
                yield return null;
            }
        }

        PixelPop.Hide(panel.gameObject);
        PixelWindows.Unregister(this);
        Unfreeze();
        PixelStats.Best(Stage1Key, 1d);
        if (second)
        {
            PixelStats.Best(Stage2Key, 1d);
            replaying = false;
            StartVendors(false);
        }

        for (float t = 0f; t < slideSeconds; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / slideSeconds);
            characterRect.anchoredPosition = new Vector2(Mathf.Lerp(restX, hiddenX, k), barH + 24f + Mathf.Abs(Mathf.Sin(t * 9f)) * 10f);
            Glance();
            yield return null;
        }
        if (canvasRoot != null) Destroy(canvasRoot);
        canvasRoot = null;
        visiting = false;
        calmTimer = 0f;
    }

    /// <summary>Hands over the gift (a few random unlocked pixel types, random amounts) and returns the text listing it.</summary>
    private string GiveGift()
    {
        var candidates = new System.Collections.Generic.List<int>();
        for (int i = 0; i < clicker.Tiers.Length; i++)
        {
            PixelClicker.PixelTier t = clicker.Tiers[i];
            if (t.unlocked && !t.rareDrop && !t.flyAway && !PixelClicker.IsStandalone(t.type)) candidates.Add(i);
        }
        StringBuilder sb = new StringBuilder();
        for (int n = 0; n < giftTypes && candidates.Count > 0; n++)
        {
            int pick = Random.Range(0, candidates.Count);
            int tier = candidates[pick];
            candidates.RemoveAt(pick);
            int amount = Random.Range(giftMin, Mathf.Max(giftMin, giftMax) + 1);
            clicker.AddCurrency(tier, amount);
            if (sb.Length > 0) sb.Append('\n');
            sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(Color.Lerp(clicker.Tiers[tier].UIColor, Color.white, 0.4f))).Append("><b>+")
              .Append(PixelClicker.FormatNumberShort(amount)).Append(' ').Append(clicker.Tiers[tier].displayName).Append("</b></color>");
        }
        PixelAudio.Play("purchase");
        PixelHints.Announce(characterName + " gave you a gift pack!");
        return sb.ToString();
    }

    private void Freeze()
    {
        if (froze || Time.timeScale <= 0f) return;
        savedTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        froze = true;
    }

    private void Unfreeze()
    {
        if (!froze) return;
        froze = false;
        if (Time.timeScale <= 0f) Time.timeScale = savedTimeScale;
    }

    /// <summary>The suspicious eyes: the pupils dart from one side to the other every so often.</summary>
    private void Glance()
    {
        if (pupilL == null) return;
        float phase = Mathf.Sin(Time.unscaledTime * 1.7f);
        float x = phase > 0.3f ? 10f : phase < -0.3f ? -10f : 0f;
        pupilL.anchoredPosition = new Vector2(-42f + x, pupilL.anchoredPosition.y);
        pupilR.anchoredPosition = new Vector2(42f + x, pupilR.anchoredPosition.y);
    }

    // ------------------------------------------------------------------
    // Drawing
    // ------------------------------------------------------------------

    private static RectTransform Block(Transform parent, string name, Color color, Vector2 size, Vector2 pos, float rotation = 0f)
    {
        GameObject g = new GameObject(name, typeof(RectTransform), typeof(Image));
        g.transform.SetParent(parent, false);
        Image im = g.GetComponent<Image>();
        im.color = color;
        im.raycastTarget = false;
        RectTransform r = g.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = size;
        r.anchoredPosition = pos;
        r.localRotation = Quaternion.Euler(0f, 0f, rotation);
        return r;
    }

    private void BuildUI()
    {
        if (canvasRoot != null) Destroy(canvasRoot);
        PixelUIKit.EnsureEventSystem();
        canvasRoot = PixelUIKit.CreateCanvas("PixelGuide Canvas", 705, new Vector2(1920f, 1080f), true);

        GameObject holder = new GameObject("Cubie", typeof(RectTransform));
        holder.transform.SetParent(canvasRoot.transform, false);
        characterRect = holder.GetComponent<RectTransform>();
        float scale = characterHeight / 400f;
        characterRect.anchorMin = characterRect.anchorMax = new Vector2(1f, 0f);
        characterRect.pivot = new Vector2(0.5f, 0f);
        characterRect.sizeDelta = new Vector2(300f * scale, 400f * scale);

        GameObject art = new GameObject("Art", typeof(RectTransform));
        art.transform.SetParent(holder.transform, false);
        RectTransform ar = art.GetComponent<RectTransform>();
        ar.anchorMin = ar.anchorMax = new Vector2(0.5f, 0f);
        ar.pivot = new Vector2(0.5f, 0f);
        ar.sizeDelta = new Vector2(300f, 400f);
        ar.localScale = Vector3.one * scale;
        DrawCharacter(art.transform);

        // Speech box.
        GameObject pg = new GameObject("Speech", typeof(RectTransform), typeof(Image), typeof(PixelPop));
        pg.transform.SetParent(canvasRoot.transform, false);
        pg.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.16f, 0.97f);
        panel = pg.GetComponent<RectTransform>();
        panel.anchorMin = panel.anchorMax = new Vector2(1f, 0.5f);
        panel.pivot = new Vector2(1f, 0.5f);
        panel.sizeDelta = new Vector2(760f, 500f);
        panel.anchoredPosition = new Vector2(-(characterRect.sizeDelta.x + 130f), 0f);

        TMP_Text title = PixelUIKit.CreateText(Font, panel, "Name", characterName, 56f, TextAlignmentOptions.Center, FontStyles.Bold, new Color(0.5f, 0.95f, 0.85f));
        PixelUIKit.Caps(title);
        SetRect(title.rectTransform, 0f, 1f, 1f, 1f, new Vector2(20f, -84f), new Vector2(-20f, -16f));

        message = PixelUIKit.CreateText(Font, panel, "Message", "", 34f, TextAlignmentOptions.Center, FontStyles.Normal, Color.white);
        message.richText = true;
        message.enableAutoSizing = true;
        message.fontSizeMax = 34f;
        message.fontSizeMin = 18f;
        SetRect(message.rectTransform, 0f, 0f, 1f, 1f, new Vector2(36f, 130f), new Vector2(-36f, -96f));

        nextButton = PixelUIKit.CreateButton(Font, panel, "Next", nextText, new Vector2(300f, 80f), new Color(0.2f, 0.55f, 0.5f, 1f), Color.white, 38f);
        PixelUIKit.Caps(nextButton);
        nextLabel = nextButton.GetComponentInChildren<TMP_Text>();
        RectTransform br = nextButton.GetComponent<RectTransform>();
        br.anchorMin = br.anchorMax = new Vector2(0.5f, 0f);
        br.pivot = new Vector2(0.5f, 0f);
        br.anchoredPosition = new Vector2(0f, 30f);
        nextButton.onClick.AddListener(() => advance++);
        panel.gameObject.SetActive(false);
    }

    private static void SetRect(RectTransform r, float minX, float minY, float maxX, float maxY, Vector2 offsetMin, Vector2 offsetMax)
    {
        r.anchorMin = new Vector2(minX, minY);
        r.anchorMax = new Vector2(maxX, maxY);
        r.offsetMin = offsetMin;
        r.offsetMax = offsetMax;
    }

    /// <summary>Cubie: a teal cube with narrowed, sideways-glancing eyes and a crooked smirk.</summary>
    private void DrawCharacter(Transform a)
    {
        Color face = new Color(0.22f, 0.62f, 0.6f), top = new Color(0.45f, 0.82f, 0.78f), side = new Color(0.14f, 0.42f, 0.42f);
        Color dark = new Color(0.06f, 0.1f, 0.12f), white = new Color(0.97f, 0.97f, 0.9f), gold = new Color(1f, 0.82f, 0.25f);
        Color red = new Color(0.85f, 0.22f, 0.25f);

        Block(a, "Leg L", side, new Vector2(40f, 50f), new Vector2(-45f, 28f));
        Block(a, "Leg R", side, new Vector2(40f, 50f), new Vector2(45f, 28f));
        Block(a, "Shoe L", dark, new Vector2(58f, 20f), new Vector2(-48f, 10f));
        Block(a, "Shoe R", dark, new Vector2(58f, 20f), new Vector2(48f, 10f));
        Block(a, "Arm L", side, new Vector2(34f, 100f), new Vector2(-122f, 150f), 8f);
        Block(a, "Arm R", side, new Vector2(34f, 100f), new Vector2(122f, 150f), -8f);
        Block(a, "Side", side, new Vector2(200f, 200f), new Vector2(12f, 190f));
        Block(a, "Face", face, new Vector2(200f, 200f), new Vector2(0f, 180f));
        Block(a, "Top", top, new Vector2(200f, 26f), new Vector2(6f, 290f));
        Block(a, "Hat Brim", dark, new Vector2(150f, 14f), new Vector2(0f, 300f));
        Block(a, "Hat Top", dark, new Vector2(100f, 40f), new Vector2(0f, 326f));
        Block(a, "Hat Band", red, new Vector2(100f, 10f), new Vector2(0f, 312f));
        Block(a, "Eye L", white, new Vector2(56f, 38f), new Vector2(-42f, 200f));
        Block(a, "Eye R", white, new Vector2(56f, 38f), new Vector2(42f, 200f));
        pupilL = Block(a, "Pupil L", dark, new Vector2(18f, 26f), new Vector2(-42f, 198f));
        pupilR = Block(a, "Pupil R", dark, new Vector2(18f, 26f), new Vector2(42f, 198f));
        Block(a, "Lid L", face, new Vector2(60f, 20f), new Vector2(-42f, 213f));
        Block(a, "Lid R", face, new Vector2(60f, 20f), new Vector2(42f, 213f));
        Block(a, "Brow L", dark, new Vector2(70f, 10f), new Vector2(-42f, 228f), -14f);
        Block(a, "Brow R", dark, new Vector2(70f, 10f), new Vector2(42f, 228f), 14f);
        Block(a, "Mouth", dark, new Vector2(70f, 9f), new Vector2(-6f, 140f), 6f);
        Block(a, "Smirk", dark, new Vector2(10f, 18f), new Vector2(34f, 150f));

        // The gift he holds up on visit 2.
        GameObject gift = new GameObject("Gift", typeof(RectTransform));
        gift.transform.SetParent(a, false);
        giftBox = gift.GetComponent<RectTransform>();
        giftBox.anchorMin = giftBox.anchorMax = new Vector2(0.5f, 0f);
        giftBox.sizeDelta = Vector2.zero;
        giftBox.anchoredPosition = Vector2.zero;
        Block(giftBox, "Box", red, new Vector2(76f, 60f), new Vector2(-150f, 225f), 8f);
        Block(giftBox, "Ribbon V", gold, new Vector2(12f, 60f), new Vector2(-150f, 225f), 8f);
        Block(giftBox, "Ribbon H", gold, new Vector2(76f, 12f), new Vector2(-150f, 225f), 8f);
        Block(giftBox, "Bow", gold, new Vector2(26f, 18f), new Vector2(-152f, 262f), 8f);
        giftBox.gameObject.SetActive(false);
    }
}
