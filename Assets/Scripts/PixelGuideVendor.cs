// ---- System notes (moved here from CLAUDE.md) ----
// PixelGuideVendor.cs
//   Cubie's intro visits only start when nothing else is open (Calm: no window in PixelWindows, no tip box, no minigame...) and only while 'Show first-time tips' (PixelHints.TipsEnabled) is on: with tips
//   off the visits are skipped, their progress is recorded and the starter gift is handed over quietly once the shop exists (Update); Replay tutorial still plays them.
//   Cubie, the suspicious cube guide / shopkeeper (added by PixelClicker.Awake; code-drawn teal cube with darting sideways eyes, hat,
//   smirk; slides in like the visitors, time stops, a paged speech box, Next button or Escape = next page). Visit 1 (new game, after
//   the intro tips, calmSeconds (1.5) of calm): a shady dealer's pitch - pixels are money 'because that's what we're all told' (a
//   fiat-currency joke), clicking is the monotonous grind you endure until the good stuff opens up - then he leaves and you click
//   pixels (4 pages; visit 2 has 6 and ends on the same theme: the clicking never stops, it just pays better). Visit 2 (when
//   PixelShop.ShopAvailable): introduces the shop, hands over a gift (giftTypes random unlocked pixel types, giftMin..giftMax each,
//   given when the page containing {gift} shows, a gift box appears in his hand), says more vendors will come and can be switched off
//   in the Toggles menu, mentions the Wizard. Progress = stat counters guide.stage1/2 (saved with the game); a save that already has
//   the shop gets no visits and no gift (stages set silently). PixelGuideVendor.Pending/Visiting hold back the shop tutorial; Settings
//   > Replay tutorial calls PixelGuideVendor.Replay() (clears the stage counters, both visits play again, no second gift). The vendors
//   are free now: the Wizard / Tinkerer / Pixel Entrepreneur packs were removed from the shop (MigrateOldPackData drops them from
//   saved lists); the guide starts them (StartVendors: Wizard right after visit 2 with a first visit in wizardFirstVisitMin..Max s,
//   Farmer at farmerAtTiers (5), Tinkerer at tinkererAtTiers (6) unlocked pixel types, Entrepreneur at entrepreneurAtTiers (8); a
//   vendor the player switched off in Toggles is never restarted; PixelVisitorMinigame.ScheduleFirstVisit). All texts / numbers are
//   Inspector fields (disableGuide turns it all off). The page lists (visitOnePages / visitTwoPages) are filled with the built-in lines when empty
//   (EnsureDefaultPages in Awake and the editor's OnValidate, like PixelHints), so Cubie's lines are edited in the Inspector.
//   Visit 1 waits until visitOneAfterWhiteClicks (50) White pixels have been clicked (the White tier's timesCollected). A visit-1 page containing {recycler} hands over a Recycler
//   (PixelConsumables.GiveDevice; text recyclerGiftText); a page list without the token still gets it when the visit ends.
// ---------------------------------------------------
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

    [Min(0)]
    [Tooltip("Visit 1 waits until this many White pixels have been clicked (so Cubie does not turn up the moment the game starts).")]
    [SerializeField] private int visitOneAfterWhiteClicks = 50;

    /// <summary>How many White pixels have been clicked so far (PixelTier.timesCollected, saved).</summary>
    private long WhiteClicks()
    {
        PixelClicker c = PixelFind.First<PixelClicker>();
        int i = c != null ? c.IndexOf(PixelClicker.PixelType.White) : -1;
        return i >= 0 ? c.Tiers[i].timesCollected : 0L;
    }

    [Tooltip("Text that replaces {recycler} on the page where Cubie hands over the Recycler (visit 1).")]
    [SerializeField] private string recyclerGiftText = "<b>+1 Recycler</b> (in your Consumables)";

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
    [Tooltip("Visit 1, one entry per page. Filled with the built-in lines when empty, so they can be edited here.")]
    [SerializeField] private string[] visitOnePages = new string[0];

    [TextArea(2, 4)]
    [Tooltip("Visit 2, one entry per page. The page that contains {gift} shows the gift and is when it is handed over. Filled with the built-in lines when empty, so they can be edited here.")]
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
    private static void ResetStatics()
    {
        instance = null; PointOverride = null; ShopHold = false;
        UnityEngine.SceneManagement.SceneManager.sceneUnloaded -= OnSceneUnloaded;
        UnityEngine.SceneManagement.SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    // (Restart reloads the scene: a tutorial's pointer callback into the destroyed old scene, or a shop hold, must not carry over)
    private static void OnSceneUnloaded(UnityEngine.SceneManagement.Scene scene) { PointOverride = null; ShopHold = false; }

    private const string Stage1Key = "guide.stage1";
    private const string Stage2Key = "guide.stage2";

    /// <summary>True while the guide still has to show visit 2 (the shop tutorial waits for it).</summary>
    public static bool Pending => instance != null && !instance.disableGuide && PixelStats.Total(Stage2Key) < 1d;

    /// <summary>
    /// A visit is due RIGHT NOW (its trigger is met: visit 1's White clicks, or the shop for visit 2) and only waits for a calm moment. Tips and popups
    /// step aside for this - not for the whole stretch before visit 2 (Pending), which held back e.g. Standard's level 2 popup until after the shop intro.
    /// </summary>
    public static bool AboutToVisit
    {
        get
        {
            if (instance == null || instance.disableGuide || !PixelHints.TipsEnabled) return false;
            if (PixelStats.Total(Stage2Key) >= 1d) return false;
            bool s1 = PixelStats.Total(Stage1Key) >= 1d;
            return instance.ShopThere || (!s1 && instance.WhiteClicks() >= instance.visitOneAfterWhiteClicks);
        }
    }

    /// <summary>Settings > Replay tutorial: Cubie's two visits play again (without a second gift).</summary>
    public static void Replay()
    {
        if (instance == null) return;
        PixelStats.Clear(Stage1Key);
        PixelStats.Clear(Stage2Key);
        PixelStats.Clear(PixelShopTutorial.DoneKey);
        PixelStats.Clear(PixelSkills.TutorialDoneKey);
        PixelTogglesTutorial.Replay();
        instance.replaying = true;
        instance.calmTimer = 0f;
    }

    /// <summary>The shop tutorial asks Cubie to stand at the bottom right and narrate it (no speech box: the tutorial's own Got it boxes do the talking).</summary>
    public static void BeginNarration() { if (instance != null) instance.StartNarration(); }

    /// <summary>The tutorial is over: Cubie slides away again.</summary>
    public static void EndNarration() { if (instance != null) instance.StopNarration(); }

    private bool narrating;
    private RectTransform armL, pointer;
    private Coroutine narrateRoutine;
    private const float NarratorScale = 0.85f;
    private float reactTime = -10f;

    /// <summary>A new tutorial step: Cubie hops excitedly.</summary>
    public static void NarratorReact() { if (instance != null) instance.reactTime = Time.unscaledTime; }

    private void StartNarration()
    {
        if (disableGuide || visiting || narrating) return;
        narrating = true;
        narrateRoutine = StartCoroutine(NarrateRoutine());
    }

    /// <summary>Where the stick points: the middle of the shop window as it is on screen (screen pixels = overlay world units).</summary>
    /// <summary>A tutorial can aim the stick at something else than the shop window (screen pixels); null = the shop.</summary>
    public static System.Func<Vector3?> PointOverride;

    private Vector3 ShopPointTarget()
    {
        if (PointOverride != null) { Vector3? o = PointOverride(); if (o.HasValue) return o.Value; }
        if (shop != null && shop.PanelRect != null && shop.PanelRect.gameObject.activeInHierarchy)
        {
            Vector3[] c = new Vector3[4];
            shop.PanelRect.GetWorldCorners(c);
            return (c[0] + c[2]) * 0.5f;
        }
        return new Vector3(Screen.width * 0.4f, Screen.height * 0.6f, 0f);
    }

    private void StopNarration()
    {
        narrating = false;
    }

    private IEnumerator NarrateRoutine()
    {
        BuildUI();
        characterRect.localScale = Vector3.one * NarratorScale;
        float barH = PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f;
        float w = characterRect.sizeDelta.x * NarratorScale;
        float restX = -(w * 0.5f + 30f);
        float hiddenX = w * 0.5f + 40f;
        float y = barH + 16f;
        for (float t = 0f; t < slideSeconds && narrating; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / slideSeconds);
            characterRect.anchoredPosition = new Vector2(Mathf.Lerp(hiddenX, restX, k), y + Mathf.Abs(Mathf.Sin(t * 9f)) * 8f);
            Glance();
            yield return null;
        }
        // He stands still and points at the shop with a stick (no hopping); on a new step he taps the stick.
        if (armL != null) armL.gameObject.SetActive(false);
        if (pointer != null) pointer.gameObject.SetActive(true);
        while (narrating)
        {
            float now = Time.unscaledTime;
            float sinceReact = now - reactTime;
            bool tapping = sinceReact >= 0f && sinceReact < 0.9f;
            float breathe = 1f + Mathf.Sin(now * 2.4f) * 0.012f;
            characterRect.localScale = new Vector3(NarratorScale, NarratorScale * breathe, 1f);
            characterRect.localRotation = Quaternion.identity;
            characterRect.anchoredPosition = new Vector2(restX, y);
            if (pointer != null)
            {
                Vector3 target = ShopPointTarget();
                Vector2 dir = (Vector2)(target - pointer.position);
                float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
                ang += Mathf.Sin(now * 1.6f) * 2f + (tapping ? Mathf.Sin(sinceReact * 22f) * 5f * (1f - sinceReact / 0.9f) : 0f);
                pointer.rotation = Quaternion.Euler(0f, 0f, ang);
                pointer.localScale = new Vector3(1f, tapping ? 1f + Mathf.Sin(sinceReact * 22f) * 0.05f : 1f, 1f);
            }
            Glance();
            yield return null;
        }
        if (pointer != null) pointer.gameObject.SetActive(false);
        if (armL != null) armL.gameObject.SetActive(true);
        characterRect.localRotation = Quaternion.identity;
        float from = characterRect != null ? characterRect.anchoredPosition.x : restX;
        for (float t = 0f; t < slideSeconds * 0.7f && characterRect != null; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / (slideSeconds * 0.7f));
            characterRect.anchoredPosition = new Vector2(Mathf.Lerp(from, hiddenX, k), y);
            yield return null;
        }
        if (!visiting && canvasRoot != null) Destroy(canvasRoot);
        narrateRoutine = null;
    }

    /// <summary>True while he is on screen.</summary>
    public static bool Visiting => instance != null && instance.visiting;

    /// <summary>
    /// After his second visit time stays frozen until the player opens the shop (the 'Buy the RGB Pack' line pulses and arrows point at the Shop button,
    /// see PixelShopNudge); opening it starts the shop tutorial. Stops a player who fiddles too fast from skipping the tutorial.
    /// </summary>
    public static bool ShopHold { get; private set; }

    private float holdScale = 1f;

    // ---- shop peek: while ShopHold is on Cubie sticks out from under the Shop button (top right) and points his stick up at it until the shop is opened ----
    private bool peeking;
    private Coroutine peekRoutine;
    private const float PeekScale = 0.6f, StickLength = 422f;   // (stick length in his own drawing units: arm + stick + tip)

    private IEnumerator PeekRoutine()
    {
        peeking = true;
        BuildUI();
        characterRect.anchorMin = characterRect.anchorMax = new Vector2(1f, 1f);
        characterRect.pivot = new Vector2(0.5f, 1f);   // his top edge hangs from the point under the button
        characterRect.localScale = Vector3.one * PeekScale;
        if (armL != null) armL.gameObject.SetActive(false);
        if (pointer != null) pointer.gameObject.SetActive(true);
        Canvas canvas = canvasRoot.GetComponent<Canvas>();
        float slide = 0f;
        while (ShopHold && characterRect != null)
        {
            slide = Mathf.MoveTowards(slide, 1f, Time.unscaledDeltaTime / Mathf.Max(0.2f, slideSeconds * 0.5f));
            float k = Mathf.SmoothStep(0f, 1f, slide);
            RectTransform button = shop != null ? shop.ShopButtonRect : null;
            float scale = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            float w = characterRect.sizeDelta.x * PeekScale;
            Vector3[] c = new Vector3[4];
            if (button != null) button.GetWorldCorners(c);
            else { c[0] = new Vector3(Screen.width - 200f, Screen.height - 120f, 0f); c[2] = c[0] + new Vector3(160f, 80f, 0f); }
            Vector2 buttonMid = (c[0] + c[2]) * 0.5f;
            // Resting spot: just under the button, kept inside the screen; he slides in from the right edge.
            float restX = Mathf.Min(buttonMid.x - 40f, Screen.width - w * 0.5f * scale - 10f * scale);
            float x = Mathf.Lerp(Screen.width + w * 0.5f * scale, restX, k);
            float now = Time.unscaledTime;
            characterRect.anchoredPosition = new Vector2((x - Screen.width) / scale, (c[0].y - 10f * scale - Screen.height) / scale + Mathf.Sin(now * 2.4f) * 2f);
            characterRect.localRotation = Quaternion.identity;
            if (pointer != null)
            {
                Vector3 target = new Vector3(buttonMid.x, c[0].y + (c[2].y - c[0].y) * 0.3f, 0f);
                Vector2 dir = (Vector2)(target - pointer.position);
                float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f + Mathf.Sin(now * 1.6f) * 2f;
                pointer.rotation = Quaternion.Euler(0f, 0f, ang);
                // The stick is only as long as the way to the button, so it touches it instead of poking past it; it taps now and then.
                float reach = pointer.lossyScale.x > 0f ? dir.magnitude / (StickLength * pointer.lossyScale.x) : 1f;
                float tap = Mathf.Max(0f, Mathf.Sin(now * 3.2f)) * 0.06f;
                pointer.localScale = new Vector3(1f, Mathf.Clamp(reach, 0.3f, 1f) * (1f - tap), 1f);
            }
            Glance();
            yield return null;
        }
        // The shop was opened: he slips back out to the right.
        if (pointer != null) pointer.gameObject.SetActive(false);
        if (armL != null) armL.gameObject.SetActive(true);
        if (characterRect != null)
        {
            float from = characterRect.anchoredPosition.x, to = characterRect.sizeDelta.x * PeekScale + 40f;
            for (float t = 0f; t < 0.4f && characterRect != null; t += Time.unscaledDeltaTime)
            {
                characterRect.anchoredPosition = new Vector2(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / 0.4f)), characterRect.anchoredPosition.y);
                yield return null;
            }
        }
        if (!visiting && !narrating && canvasRoot != null) Destroy(canvasRoot);
        peeking = false;
        peekRoutine = null;
    }

    private void UpdateHold()
    {
        bool leave = PixelTitleScreen.Showing || !PixelHints.TipsEnabled || shop == null || !shop.ShopAvailable || PixelStats.Total(PixelShopTutorial.DoneKey) >= 1d;
        if (leave || shop.WindowOpen)
        {
            ShopHold = false;
            if (Time.timeScale <= 0f && !PixelCalm.InMenu) Time.timeScale = holdScale;
            if (!leave) PixelShopTutorial.RequestStart();
            return;
        }
        if (!PixelPauseMenu.IsPaused && Time.timeScale > 0f)
        {
            holdScale = Time.timeScale;
            Time.timeScale = 0f;
        }
    }


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

    /// <summary>Copies the built-in pages into empty page lists, so the Inspector shows Cubie's real lines and they can be edited there (like the first-time tips).</summary>
    private bool EnsureDefaultPages()
    {
        bool changed = false;
        if (visitOnePages == null || visitOnePages.Length == 0) { visitOnePages = DefaultOne; changed = true; }
        if (visitTwoPages == null || visitTwoPages.Length == 0) { visitTwoPages = DefaultTwo; changed = true; }
        return changed;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying) return;
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || Application.isPlaying) return;
            if (EnsureDefaultPages()) UnityEditor.EditorUtility.SetDirty(this);
        };
    }
#endif

    private void Awake()
    {
        instance = this;
        EnsureDefaultPages();
        if (characterName == "Cubby") characterName = "Cubie"; // a scene saved before the rename
        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
        if (shop == null) shop = PixelFind.First<PixelShop>();
    }

    private void OnDestroy()
    {
        Unfreeze();
        if (ShopHold) { ShopHold = false; if (Time.timeScale <= 0f) Time.timeScale = holdScale; }
        PixelWindows.Unregister(this);
        if (instance == this) instance = null;
        if (canvasRoot != null) Destroy(canvasRoot);
    }

    private bool Calm() => CalmBlocker() == null;

    private string lastBlocker;

    /// <summary>What keeps Cubie from turning up right now (null = nothing: it is calm). The reason is logged once whenever it changes (Editor / dev builds, PixelDebug) so a missing visit can be traced.</summary>
    private string CalmBlocker()
    {
        string why = null;
        if (PixelTitleScreen.Showing) why = "the title screen is showing";
        else if (PixelHints.IntroHoldsGuide) why = "the intro tips are still running";
        else if (PixelNotice.IsShowing) why = "a tip box is showing";
        else if (PixelPauseMenu.IsPaused) why = "the game is paused";
        else if (PixelMinigame.TakeoverActive) why = "a takeover minigame is running";
        else if (PixelPets.PopupOpen) why = "a pet popup is open";
        else if (PixelCameraIntro.ClicksLocked) why = "the camera intro has clicks locked";
        else if (PixelCameraIntro.Moving) why = "the camera intro is moving";
        else if (Time.timeScale <= 0f) why = "Time.timeScale is 0";
        else if (PixelWindows.AnyOpen()) why = "a registered window is open";   // (Cubie only turns up when no other window is open)
        else
            foreach (PixelMinigame m in PixelMinigame.All) if (m != null && m.Busy) { why = "the minigame " + m.Id + " is busy"; break; }
        if (why != lastBlocker)
        {
            lastBlocker = why;
            PixelDebug.Info("Cubie waits: " + (why ?? "calm now"), this);
        }
        return why;
    }

    private bool ShopThere => (shop != null ? shop : (shop = PixelFind.First<PixelShop>())) != null && shop.ShopAvailable;

    private void Update()
    {
        if (ShopHold) UpdateHold();
        if (ShopHold && !peeking && !disableGuide && !visiting && !narrating) peekRoutine = StartCoroutine(PeekRoutine());
        if (disableGuide || clicker == null || visiting) { if (disableGuide && lastBlocker != "disabled") { lastBlocker = "disabled"; PixelDebug.Info("Cubie is off: disableGuide is ticked", this); } return; }
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
                PixelDebug.Info("Cubie skips both visits: the Shop button is already available (required pixel unlocked)", this);
                PixelStats.Best(Stage1Key, 1d);
                PixelStats.Best(Stage2Key, 1d);
                PixelStats.Best(PixelShopTutorial.DoneKey, 1d);   // an older save: no tutorial
                StartVendors(true);
            }
            else if (!PixelHints.TipsEnabled && !replaying)
            {
                PixelDebug.Info("Cubie skips his visits: Show first-time tips is off", this);
                // 'Show first-time tips' is off: Cubie does not turn up with his intros. The game carries on as if they had happened, and the starter gift still arrives once the shop is there.
                if (!s1) PixelStats.Best(Stage1Key, 1d);
                if (ShopThere)
                {
                    PixelStats.Best(Stage2Key, 1d);
                    GiveGift();
                    StartVendors(false);
                }
            }
            else if (ShopThere) StartCoroutine(Visit(true));
            else if (!s1 && WhiteClicks() < visitOneAfterWhiteClicks) { if (lastBlocker != "clicks") { lastBlocker = "clicks"; PixelDebug.Info("Cubie visit 1 waits for " + visitOneAfterWhiteClicks + " White pixel clicks", this); } }
            else if (!s1) { PixelDebug.Info("Cubie starts visit 1", this); StartCoroutine(Visit(false)); }
            else if (lastBlocker != "s1done") { lastBlocker = "s1done"; PixelDebug.Info("Cubie visit 1 is already recorded (guide.stage1) - he waits for the shop", this); }
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
        // The Prospector buys mining goods and only visits while Mining is the skill in training (he is started as soon as Mining is available).
        PixelSkills skills = PixelSkills.Instance;
        TryStart("lumberjack", skills != null && skills.IsAvailable(skills.Find(PixelSkills.WoodcuttingId)), Random.Range(otherFirstVisitSeconds * 0.5f, otherFirstVisitSeconds));   // the Lumberjack buys wood goods and only visits while Woodcutting is trained
        TryStart("prospector", skills != null && skills.IsAvailable(skills.Find(PixelSkills.MiningId)), Random.Range(otherFirstVisitSeconds * 0.5f, otherFirstVisitSeconds));
        // Two very rare cat visitors, Egg and Bean: a special gift the first time, a random present of pixels on later visits.
        TryStart("egg", UnlockedTypes() >= 4, Random.Range(600f, 1200f));
        TryStart("bean", UnlockedTypes() >= 6, Random.Range(900f, 1500f));
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
        "Psst... over here. The name's Cubie. I deal in pixels. A very legitimate little operation.",
        "See, pixels are money around here. The white, gray and black ones, then red, green and blue later. Why those? Why not the shiny ones? Who knows. That's just what we're all told, so we go with it.",
        "Now, how do you get them? You click. And click. And click some more. Same thing, all day. Don't look at me like that, everybody starts at the bottom.",
        "Oh, and those leftover bits piling up on the floor? Don't just let them rot. Take this Recycler: toss them in, hit the button, and you get a little something back.\n\n{recycler}",
        "Think of it as paying your dues. Tedious? Absolutely. But grind long enough and the good stuff opens up. Come find me when you've got a pile worth my time.",
    };

    private string[] DefaultTwo => new[]
    {
        "Well, well... look at that pile. Real, official, everybody-agrees-it's-worth-something pixels. All that clicking paid off, huh? Welcome to my shop. The Shop button's at the top right. Don't worry, I'm not counting your pockets. Much.",
        "Here, a little starter gift. Nothing suspicious about it. Don't ask what it's backed by.\n\n{gift}",
        "Here's the secret nobody tells you: the clicking never really stops. It just gets better pay. Spend your pure pixels on upgrades and gadgets, and soon the work starts doing itself while you do the clever bits.",
        "The fancy pixels, ore, crops and sparks, are goods, not coin. Worth plenty to the right buyer, worthless to the wrong one. Learn a skill and you'll meet the buyers.",
        "More vendors will wander through as you grow. Where from? Nobody asks. They pay in the same pixels, and that's good enough for everyone. You can switch them off in the Toggles menu (it opens up once you get the Auto Clicker).",
        "My friend the Wizard might drop by soon. Keep him away from my stock, he turns things into frogs. Be seeing you...",
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

        giftLine = null;
        for (int i = 0; i < pages.Length; i++)
        {
            wantBack = false;
            if (backButton != null) backButton.gameObject.SetActive(i > 0);
            string text = pages[i];
            if (text.Contains("{recycler}"))
            {
                if (!recyclerGiven && !replaying) { recyclerGiven = true; GiveRecycler(); }
                text = text.Replace("{recycler}", recyclerGiftText);
            }
            if (text.Contains("{gift}"))
            {
                if (giftLine == null) giftLine = replaying ? "(You already got yours, no seconds!)" : GiveGift();   // handed over once, even if the player goes back to this page
                text = text.Replace("{gift}", giftLine);
                if (giftBox != null) giftBox.gameObject.SetActive(true);
            }
            PixelUIKit.SetText(message, text);
            PixelUIKit.SetText(nextLabel, i == pages.Length - 1 ? lastText : nextText);
            int start = advance;

            // The page types itself out; Next while it types shows the rest at once, Next again turns the page.
            message.ForceMeshUpdate();
            int total = message.textInfo.characterCount;
            float shown = 0f, speed = typeCharsPerSecond > 0f ? typeCharsPerSecond : 55f;
            message.maxVisibleCharacters = 0;
            while (true)
            {
                if (wantBack) break;
                if (advance != start)
                {
                    if (shown < total) { shown = total; message.maxVisibleCharacters = total; start = advance; }
                    else break;
                }
                else if (shown < total)
                {
                    int wasShown = (int)shown;
                    shown += Time.unscaledDeltaTime * speed;
                    message.maxVisibleCharacters = Mathf.Min(total, (int)shown);
                    PixelTalk.Typed(message, wasShown, Mathf.Min(total, (int)shown), 0.92f);   // Cubie's voice: talking blips, a touch lower
                }

                characterRect.anchoredPosition = new Vector2(restX, barH + 24f + Mathf.Sin(Time.unscaledTime * 3f) * 4f);
                Glance();
                bool done = shown >= total;
                nextButton.transform.localScale = Vector3.one * (done ? 1f + 0.05f * Mathf.Sin(Time.unscaledTime * 6f) : 1f);   // the button pulses once the page is complete
                if (nameLabel != null)
                    nameLabel.color = Color.Lerp(new Color(0.5f, 0.95f, 0.85f), new Color(0.8f, 1f, 0.92f), 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.5f));   // the name shimmers
                yield return null;
            }
            message.maxVisibleCharacters = int.MaxValue;
            nextButton.transform.localScale = Vector3.one;
            if (wantBack) { wantBack = false; i = Mathf.Max(-1, i - 2); }   // one page back (the loop adds 1)
        }

        PixelPop.Hide(panel.gameObject);
        PixelWindows.Unregister(this);
        Unfreeze();
        if (!second && !recyclerGiven && !replaying) { recyclerGiven = true; GiveRecycler(); }   // (a page list without {recycler} still hands it over)
        PixelStats.Best(Stage1Key, 1d);
        if (second)
        {
            PixelStats.Best(Stage2Key, 1d);
            if (shop != null && shop.ShopAvailable && PixelHints.TipsEnabled && PixelStats.Total(PixelShopTutorial.DoneKey) < 1d)
            {
                ShopHold = true;   // time stays frozen until the player opens the shop
                holdScale = savedTimeScale > 0f ? savedTimeScale : 1f;
                if (Time.timeScale > 0f) Time.timeScale = 0f;
            }
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

    private bool recyclerGiven;

    /// <summary>Visit 1: Cubie gives the player a Recycler (it goes into the Consumables).</summary>
    private void GiveRecycler()
    {
        PixelConsumables consumables = PixelFind.First<PixelConsumables>();
        if (consumables == null || !consumables.GiveDevice(PixelConsumables.DeviceKind.Recycler, 1)) return;
        PixelAudio.Play("purchase");
        PixelHints.Announce(characterName + " gave you a Recycler! It is in your Consumables.");
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

    /// <summary>Draws Cubie (for a menu to walk about) under 'parent' at the given height: a holder anchored at the bottom centre with his art as the first child.</summary>
    public RectTransform BuildWalker(Transform parent, float height)
    {
        GameObject holder = new GameObject("Cubie Walker", typeof(RectTransform));
        holder.transform.SetParent(parent, false);
        RectTransform r = holder.GetComponent<RectTransform>();
        float scale = height / 400f;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0f);
        r.pivot = new Vector2(0.5f, 0f);
        r.sizeDelta = new Vector2(300f * scale, 400f * scale);
        GameObject art = new GameObject("Art", typeof(RectTransform));
        art.transform.SetParent(holder.transform, false);
        RectTransform ar = art.GetComponent<RectTransform>();
        ar.anchorMin = ar.anchorMax = new Vector2(0.5f, 0f);
        ar.pivot = new Vector2(0.5f, 0f);
        ar.sizeDelta = new Vector2(300f, 400f);
        ar.localScale = Vector3.one * scale;
        // DrawCharacter fills the visit's own references (pupils, arm, gift...): keep those as they were.
        RectTransform sArm = armL, sPointer = pointer, sGift = giftBox, sPL = pupilL, sPR = pupilR;
        DrawCharacter(art.transform);
        armL = sArm; pointer = sPointer; giftBox = sGift; pupilL = sPL; pupilR = sPR;
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
        PixelUIKit.StyleWindow(pg.GetComponent<Image>(), new Color(0.07f, 0.17f, 0.19f, 0.97f));   // the same rounded glowing frame as every window, tinted teal like Cubie
        panel = pg.GetComponent<RectTransform>();
        panel.anchorMin = panel.anchorMax = new Vector2(1f, 0.5f);
        panel.pivot = new Vector2(1f, 0.5f);
        panel.sizeDelta = new Vector2(760f, 500f);
        panel.anchoredPosition = new Vector2(-(characterRect.sizeDelta.x + 130f), 0f);

        // A little tail on the right edge, pointing at Cubie.
        GameObject tail = new GameObject("Tail", typeof(RectTransform), typeof(Image));
        tail.transform.SetParent(panel, false);
        tail.transform.SetAsFirstSibling();
        Image tailImage = tail.GetComponent<Image>();
        tailImage.color = new Color(0.07f, 0.17f, 0.19f, 0.97f);
        tailImage.raycastTarget = false;
        RectTransform tailRect = tail.GetComponent<RectTransform>();
        tailRect.anchorMin = tailRect.anchorMax = new Vector2(1f, 0.55f);
        tailRect.pivot = new Vector2(0.5f, 0.5f);
        tailRect.sizeDelta = new Vector2(46f, 46f);
        tailRect.anchoredPosition = new Vector2(8f, 0f);
        tailRect.localRotation = Quaternion.Euler(0f, 0f, 45f);

        TMP_Text title = PixelUIKit.CreateText(Font, panel, "Name", characterName, 56f, TextAlignmentOptions.Center, FontStyles.Bold, new Color(0.5f, 0.95f, 0.85f));
        nameLabel = title;
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

        // A small back arrow at the panel's bottom left: hold it to go back one page.
        backButton = PixelUIKit.CreateButton(Font, panel, "Back", "<", new Vector2(64f, 56f), new Color(0.15f, 0.4f, 0.4f, 1f), Color.white, 32f);
        PixelUIKit.UseArrowGlyph(backButton, true);
        RectTransform bkr = backButton.GetComponent<RectTransform>();
        bkr.anchorMin = bkr.anchorMax = new Vector2(0f, 0f);
        bkr.pivot = new Vector2(0f, 0f);
        bkr.anchoredPosition = new Vector2(30f, 42f);
        GameObject fillGo = new GameObject("Hold Fill", typeof(RectTransform), typeof(Image));
        fillGo.transform.SetParent(backButton.transform, false);
        fillGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.45f);
        fillGo.GetComponent<Image>().raycastTarget = false;
        RectTransform fillRect = fillGo.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
        fillRect.SetSiblingIndex(1);
        PixelHoldButton hold = backButton.gameObject.AddComponent<PixelHoldButton>();
        hold.Setup(fillRect, 0.6f, () => { hold.ResetProgress(); wantBack = true; advance++; });
        PixelTalk.AddMuteButton(Font, panel, new Vector2(56f, 56f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(104f, 42f), new Color(0.15f, 0.4f, 0.4f, 1f));   // mutes the talking blips
        panel.gameObject.SetActive(false);
    }

    private Button backButton;
    private bool wantBack;
    private string giftLine;

    private TMP_Text nameLabel;

    [Tooltip("How many letters of Cubie's speech appear per second (0 = 55). Clicking Next while it types shows the whole page at once.")]
    [SerializeField] private float typeCharsPerSecond = 0f;

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
        armL = Block(a, "Arm L", side, new Vector2(34f, 100f), new Vector2(-122f, 150f), 8f);
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

        // The pointing arm + stick he uses while narrating the shop tutorial (hidden otherwise; turned towards the shop every frame).
        GameObject ptr = new GameObject("Pointer", typeof(RectTransform));
        ptr.transform.SetParent(a, false);
        pointer = ptr.GetComponent<RectTransform>();
        pointer.anchorMin = pointer.anchorMax = new Vector2(0.5f, 0f);
        pointer.sizeDelta = Vector2.zero;
        pointer.anchoredPosition = new Vector2(-118f, 205f);
        Block(pointer, "Pointing Arm", side, new Vector2(32f, 120f), new Vector2(0f, 60f));
        Block(pointer, "Stick", new Color(0.55f, 0.36f, 0.16f), new Vector2(10f, 360f), new Vector2(0f, 230f));
        Block(pointer, "Stick Tip", gold, new Vector2(24f, 24f), new Vector2(0f, 410f), 45f);
        pointer.gameObject.SetActive(false);

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
