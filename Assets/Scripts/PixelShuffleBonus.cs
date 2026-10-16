// ---- System notes ----
// PixelShuffleBonus.cs
//   SHUFFLE BONUS (a test; component added by PixelClicker.Awake; needs Block Hold, it builds on its quick tap): TAP the right button on the FILLER block of the skill in play
//   (Driftwood in Fishing, Twig in Woodcutting... = PixelSkills.TryGetFiller) and Block Hold sends it behind the next block as usual (a tap = released within tapSeconds, 0.35). The first
//   shuffle starts a combo: a 'SHUFFLE x1' box with a bar that runs down (barSeconds, 6) shows under the top bar. The combo only GROWS when the block it was shuffled behind has been broken
//   (waiting = true until a PixelCollected arrives); when the filler comes back, shuffle it again before the bar runs out for x2, x3... (no cap yet). BREAKING the filler instead cashes the
//   combo in: bonus XP = xpPerShuffle (3) x the combo, on top of the normal XP (PixelSkills.AddShuffleXp). If the bar runs out first the filler breaks by itself (dropped from the queue, or
//   auto-collected if it is on the cube) and a smaller consolation bonus (expireFactor 0.35 of the cash-out) is paid. The combo block on the cube (the shuffled filler that came back from the queue, not a freshly rolled one) lights up gold: PixelClickerAura.ComboHighlight forces the cube's glow aura on in gold and makes it pulse. A skill switch (other filler) or the title screen ends the combo.
//   UNLOCK (2026-10-16): Standard skill tree node 'Shuffle Combo' (shuffle, after High roller on the Block Hold path; requireTreeNode off = with Block Hold).
//   Fields are Inspector values on this component (0 = the coded default); disableShuffleBonus switches the whole thing off. Not saved.
// ---------------------------------------------------
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PixelShuffleBonus : MonoBehaviour
{
    [Header("Shuffle bonus (test)")]
    [SerializeField, Tooltip("Turns the shuffle bonus off.")]
    private bool disableShuffleBonus;
    [SerializeField, Tooltip("The shuffle combo needs the Standard skill tree node 'Shuffle Combo' (the end of the Block Hold path). Off = it works as soon as Block Hold does.")]
    private bool requireTreeNode = true;
    [Min(0f)]
    [SerializeField, Tooltip("Seconds the combo bar lasts before the filler breaks by itself. Every shuffle fills it again. 0 = 6.")]
    private float barSeconds;
    [Min(0f)]
    [SerializeField, Tooltip("A right click released within this many seconds counts as a tap (a shuffle). 0 = 0.35.")]
    private float tapSeconds;
    [Min(0f)]
    [SerializeField, Tooltip("Bonus XP per shuffle in the combo, paid when you break the filler (x1 = this, x5 = five times this). 0 = 3.")]
    private float xpPerShuffle;
    [Range(0f, 1f)]
    [SerializeField, Tooltip("Share of the cash-out bonus paid when the bar runs out and the filler breaks by itself. 0 = 0.35.")]
    private float expireFactor;

    private static PixelShuffleBonus instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; }

    /// <summary>Block Hold calls this when a block was put back: 'heldFor' seconds of holding, 'tierIndex' the block.</summary>
    public static void OnRelease(int tierIndex, float heldFor)
    {
        if (instance != null) instance.Shuffled(tierIndex, heldFor);
    }

    public static float TapSeconds => instance != null && instance.tapSeconds > 0f ? instance.tapSeconds : 0.35f;

    private PixelClicker clicker;
    private int chain;                    // shuffles counted so far (0 = no combo)
    private PixelClicker.PixelType fillerType;
    private int comboTier = -1;
    private float bar;                    // seconds left
    private bool waiting;                 // the block it was shuffled behind has not been broken yet

    private float BarSeconds => barSeconds > 0f ? barSeconds : 6f;
    private float XpPerShuffle => xpPerShuffle > 0f ? xpPerShuffle : 3f;
    private float ExpireFactor => expireFactor > 0f ? expireFactor : 0.35f;

    private void Awake() { instance = this; clicker = GetComponent<PixelClicker>(); }
    private void OnEnable() { if (clicker == null) clicker = PixelFind.First<PixelClicker>(); if (clicker != null) clicker.PixelCollected += OnCollected; }
    private void OnDisable() { PixelClickerAura.ComboHighlight = false; if (clicker != null) clicker.PixelCollected -= OnCollected; }
    private void OnDestroy() { if (instance == this) instance = null; if (canvasObject != null) Destroy(canvasObject); }

    private void Shuffled(int tierIndex, float heldFor)
    {
        if (disableShuffleBonus || (requireTreeNode && !PixelSkills.HasStandardNode("shuffle")) || clicker == null || heldFor > TapSeconds || !clicker.IsValidTierIndex(tierIndex)) return;
        if (!PixelSkills.TryGetFiller(out PixelClicker.PixelType filler, out float share) || !PixelSkills.IsFillerType(clicker.Tiers[tierIndex].type)) return;   // (any filler of the pack counts: Twig, Pinecone, Moss)
        if (chain > 0 && fillerType != filler) EndCombo();   // another skill's filler: start over
        if (chain == 0) { chain = 1; fillerType = filler; comboTier = tierIndex; waiting = true; Refill(true); return; }
        if (waiting) return;   // it came back before the block in front was broken: nothing counts
        comboTier = tierIndex;
        chain++;
        waiting = true;
        Refill(true);
    }

    private void Refill(bool punch)
    {
        bar = BarSeconds;
        if (punch) { punchTime = Time.unscaledTime; PixelAudio.Play("click"); }
    }

    private void OnCollected(int tierIndex, double amount, bool automatic)
    {
        if (chain <= 0 || clicker == null || !clicker.IsValidTierIndex(tierIndex)) return;
        if (waiting) { waiting = false; punchTime = Time.unscaledTime; return; }   // the block it hides behind is broken: the next shuffle counts
        if (!PixelSkills.IsFillerType(clicker.Tiers[tierIndex].type)) return;
        // The filler itself was broken instead of shuffled again: cash the combo in.
        Pay(1f, "Shuffle x" + chain);
        EndCombo();
    }

    private void Pay(float factor, string label)
    {
        PixelSkills skills = PixelSkills.Instance;
        double xp = XpPerShuffle * chain * factor;
        if (skills != null) skills.AddShuffleXp(xp);
        PixelAudio.Play("purchase");
        PixelHints.Announce(label + ": +" + PixelClicker.FormatNumberShort(xp) + " bonus XP", label + " paid " + PixelClicker.FormatNumberShort(xp) + " bonus XP.");
    }

    private void EndCombo() { chain = 0; waiting = false; bar = 0f; }

    private void Update()
    {
        if (chain > 0)
        {
            if (PixelTitleScreen.Showing || !PixelSkills.TryGetFiller(out PixelClicker.PixelType filler, out float s) || filler != fillerType) EndCombo();
            else
            {
                bar -= Time.deltaTime;   // (scaled: it waits while the game is paused or frozen)
                if (bar <= 0f) Expire();
            }
        }
        UpdateVisual();
        // The combo block on the cube (the shuffled filler that came back, not a fresh filler) lights up gold, so it is easy to tell from an ordinary filler.
        PixelClickerAura.ComboHighlight = chain > 0 && !waiting && clicker != null && clicker.LiveCameFromQueue
                                          && clicker.IsValidTierIndex(clicker.CurrentClickTierIndex) && PixelSkills.IsFillerType(clicker.Tiers[clicker.CurrentClickTierIndex].type);
    }

    /// <summary>The bar ran out: the filler breaks by itself and pays a smaller bonus.</summary>
    private void Expire()
    {
        int index = clicker != null ? comboTier : -1;   // (the filler that was shuffled: Twig, Pinecone or Moss)
        bool onCube = !waiting && index >= 0 && clicker.CurrentClickTierIndex == index;
        int shown = chain;
        Pay(ExpireFactor, "Shuffle x" + shown + " (auto-break)");
        EndCombo();   // (before the collect below: its event must not cash in a second time)
        if (index < 0) return;
        if (onCube) clicker.AutoCollect();
        else clicker.DropQueuedTier(index);
    }

    // ---- the box ----
    private GameObject canvasObject;
    private RectTransform box, fill;
    private TMP_Text title, hint;
    private float punchTime = -10f;

    private void UpdateVisual()
    {
        bool show = chain > 0 && !PixelTitleScreen.Showing;
        if (!show) { if (canvasObject != null && canvasObject.activeSelf) canvasObject.SetActive(false); return; }
        if (canvasObject == null) Build();
        if (!canvasObject.activeSelf) canvasObject.SetActive(true);
        float k = Mathf.Clamp01(bar / BarSeconds);
        fill.anchorMax = new Vector2(k, 1f);
        fill.GetComponent<Image>().color = Color.Lerp(new Color(0.9f, 0.3f, 0.25f, 1f), new Color(0.35f, 0.85f, 0.45f, 1f), k);
        title.text = "SHUFFLE x" + chain;
        hint.text = waiting ? "Break the next block!" : "Shuffle it again, or break it to cash in";
        float since = Time.unscaledTime - punchTime;
        float pop = since < 0.3f ? 1f + 0.35f * Mathf.Sin(since / 0.3f * Mathf.PI) : 1f;
        box.localScale = Vector3.one * pop;
        float barH = PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f;
        box.anchoredPosition = new Vector2(0f, -(barH + 24f));
    }

    private void Build()
    {
        TMP_FontAsset font = clicker != null ? clicker.UIFont : null;
        canvasObject = PixelUIKit.CreateCanvas("Shuffle Bonus", 645, new Vector2(1920f, 1080f), false);
        GameObject go = new GameObject("Box", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(canvasObject.transform, false);
        Image bg = go.GetComponent<Image>();
        bg.color = new Color(0.07f, 0.09f, 0.14f, 0.92f);
        bg.raycastTarget = false;
        PixelUIKit.StyleBox(bg);
        box = go.GetComponent<RectTransform>();
        box.anchorMin = box.anchorMax = box.pivot = new Vector2(0.5f, 1f);
        box.sizeDelta = new Vector2(430f, 120f);

        title = PixelUIKit.CreateText(font, go.transform, "Title", "", 46f, TextAlignmentOptions.Center, FontStyles.Bold, new Color(1f, 0.85f, 0.35f));
        title.raycastTarget = false;
        RectTransform tr = title.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f); tr.pivot = new Vector2(0.5f, 1f);
        tr.sizeDelta = new Vector2(0f, 56f); tr.anchoredPosition = new Vector2(0f, -8f);

        hint = PixelUIKit.CreateText(font, go.transform, "Hint", "", 20f, TextAlignmentOptions.Center, FontStyles.Normal, new Color(0.85f, 0.9f, 1f));
        hint.raycastTarget = false;
        RectTransform hr = hint.rectTransform;
        hr.anchorMin = new Vector2(0f, 1f); hr.anchorMax = new Vector2(1f, 1f); hr.pivot = new Vector2(0.5f, 1f);
        hr.sizeDelta = new Vector2(0f, 28f); hr.anchoredPosition = new Vector2(0f, -62f);

        GameObject track = new GameObject("Bar Track", typeof(RectTransform), typeof(Image));
        track.transform.SetParent(go.transform, false);
        track.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);
        track.GetComponent<Image>().raycastTarget = false;
        RectTransform trk = track.GetComponent<RectTransform>();
        trk.anchorMin = new Vector2(0f, 0f); trk.anchorMax = new Vector2(1f, 0f); trk.pivot = new Vector2(0.5f, 0f);
        trk.sizeDelta = new Vector2(-32f, 14f); trk.anchoredPosition = new Vector2(0f, 12f);
        GameObject f = new GameObject("Bar Fill", typeof(RectTransform), typeof(Image));
        f.transform.SetParent(track.transform, false);
        f.GetComponent<Image>().raycastTarget = false;
        fill = f.GetComponent<RectTransform>();
        fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one;
        fill.offsetMin = fill.offsetMax = Vector2.zero;
        canvasObject.SetActive(false);
    }
}
