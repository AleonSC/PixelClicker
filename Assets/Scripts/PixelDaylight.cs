// ---- System notes ----
// PixelDaylight.cs
//   Solar's daylight bar (moved out of PixelMeteorSolar.cs into its own file, because Unity can only save a component in the scene when its
//   class lives in a file of the same name). Every Solar click raises daylight and it slowly fades; while it is up seeds and Seed pets grow
//   faster and hovering Solar husks keep it up. A small sun + bar at the top right shows it. Not saved.
// ---------------------------------------------------
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Daylight (added by <c>PixelClicker.Awake</c>): every Solar click raises it, it slowly fades. While it is up seeds and Seed pets grow faster and
/// hovering Solar pixels pay more. A small sun + bar at the top right shows it while it is above zero. Not saved (it starts at 0).
/// </summary>
public class PixelDaylight : MonoBehaviour
{
    [Header("Daylight")]
    [Tooltip("Turn the daylight mechanic off (Solar clicks no longer raise it, no bonuses, no bar).")]
    [SerializeField] private bool disableDaylight = false;
    [Tooltip("How much one Solar click raises daylight (1 = full). 0 = 0.1.")]
    [SerializeField] private float perClick = 0f;
    [Tooltip("How much daylight is lost per second. 0 = 0.012 (about 80 seconds from full).")]
    [SerializeField] private float decayPerSecond = 0f;
    [Tooltip("Extra growth speed sprouts and Seed pets get at full daylight (added to the Solar-pixel boost). 0 = 1.5.")]
    [SerializeField] private float seedGrowthBonus = 0f;
    [Tooltip("Extra pay a hovering Solar pixel's trickle gets at full daylight (2 = three times as much). 0 = 2.")]
    [SerializeField] private float hoverPayBonus = 0f;
    [Tooltip("A hovering Solar pixel pays this fraction of its value each time. 0 = 0.08.")]
    [SerializeField] private float trickleFraction = 0f;
    [Tooltip("Seconds between a hovering Solar pixel's payments. 0 = 4.")]
    [SerializeField] private float trickleSeconds = 0f;
    [Tooltip("Pixel rules (PixelClicker.husksPayCurrency off): daylight a hovering Solar husk adds every trickleSeconds instead of paying (1 = full). 0 = 0.02 (one husk slows the fade by about 40%).")]
    [SerializeField] private float hoverDaylight = 0f;

    [Header("Bar")]
    [Tooltip("Hide the daylight bar (the effects still work).")]
    [SerializeField] private bool hideBar = false;
    [Tooltip("Text on the bar.")]
    [SerializeField] private string barLabel = "Daylight";

    private static PixelDaylight instance;
    private float level;
    private PixelClicker clicker;
    private GameObject canvasRoot;
    private CanvasGroup group;
    private RectTransform panel, fill;
    private TMP_Text percent;
    private bool built;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; }

    private bool Off => disableDaylight;

    public static float Level => instance != null && !instance.Off ? instance.level : 0f;
    public static float GrowthBonus => instance != null && !instance.Off ? instance.level * (instance.seedGrowthBonus > 0f ? instance.seedGrowthBonus : 1.5f) : 0f;
    public static float PayMultiplier => instance != null && !instance.Off ? 1f + instance.level * (instance.hoverPayBonus > 0f ? instance.hoverPayBonus : 2f) : 1f;
    public static float TrickleFraction => instance != null && instance.trickleFraction > 0f ? instance.trickleFraction : 0.08f;
    public static float TrickleSeconds => instance != null && instance.trickleSeconds > 0f ? instance.trickleSeconds : 4f;

    /// <summary>A Solar pixel was clicked: daylight goes up.</summary>
    public static void OnSolarClick()
    {
        if (instance == null || instance.Off) return;
        instance.level = Mathf.Clamp01(instance.level + (instance.perClick > 0f ? instance.perClick : 0.1f));
    }

    /// <summary>Pixel rules: a hovering Solar husk ticked - daylight goes up a little (it keeps the day going instead of paying).</summary>
    public static void OnHoverTick()
    {
        if (instance == null || instance.Off) return;
        instance.level = Mathf.Clamp01(instance.level + (instance.hoverDaylight > 0f ? instance.hoverDaylight : 0.02f));
    }

    private void Awake() { instance = this; }
    private void OnDestroy() { if (instance == this) instance = null; if (canvasRoot != null) Destroy(canvasRoot); }

    private void Update()
    {
        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
        if (level > 0f && Time.timeScale > 0f) level = Mathf.Max(0f, level - (decayPerSecond > 0f ? decayPerSecond : 0.012f) * Time.deltaTime);
        if (hideBar || Off || clicker == null) { if (group != null) group.alpha = 0f; return; }
        if (!built) Build();
        if (!built) return;

        bool show = level > 0.004f && clicker.IsUnlocked(PixelClicker.PixelType.Solar) && !PixelTitleScreen.Showing;
        group.alpha = Mathf.MoveTowards(group.alpha, show ? 1f : 0f, Time.unscaledDeltaTime / 0.25f);
        if (group.alpha <= 0.001f) return;
        float bar = PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f;
        panel.anchoredPosition = new Vector2(-20f, -(bar + 14f));
        fill.anchorMax = new Vector2(Mathf.Clamp01(level), 1f);
        PixelUIKit.SetText(percent, Mathf.RoundToInt(level * 100f) + "%");
    }

    private void Build()
    {
        canvasRoot = PixelUIKit.CreateCanvas("Daylight HUD", 105, new Vector2(1920f, 1080f), false);
        group = canvasRoot.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        GameObject p = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        p.transform.SetParent(canvasRoot.transform, false);
        Image bg = p.GetComponent<Image>();
        bg.color = new Color(0.07f, 0.06f, 0.04f, 0.75f);
        bg.raycastTarget = false;
        panel = p.GetComponent<RectTransform>();
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(1f, 1f);
        panel.sizeDelta = new Vector2(270f, 58f);

        // The sun icon: two gold squares, one turned 45 degrees, makes an eight-pointed star.
        for (int i = 0; i < 2; i++)
        {
            GameObject sq = new GameObject("Sun " + i, typeof(RectTransform), typeof(Image));
            sq.transform.SetParent(p.transform, false);
            Image im = sq.GetComponent<Image>();
            im.color = i == 0 ? new Color(1f, 0.78f, 0.2f, 1f) : new Color(1f, 0.92f, 0.5f, 1f);
            im.raycastTarget = false;
            RectTransform r = sq.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 0.5f);
            r.sizeDelta = new Vector2(22f, 22f);
            r.anchoredPosition = new Vector2(32f, 0f);
            r.localRotation = Quaternion.Euler(0f, 0f, i * 45f);
        }

        TMP_Text label = PixelUIKit.CreateText(clicker.UIFont, p.transform, "Label", barLabel, 20f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, new Color(1f, 0.88f, 0.45f, 1f));
        PixelUIKit.Caps(label);
        label.raycastTarget = false;
        RectTransform lr = label.rectTransform;
        lr.anchorMin = new Vector2(0f, 1f); lr.anchorMax = new Vector2(1f, 1f); lr.pivot = new Vector2(0f, 1f);
        lr.sizeDelta = new Vector2(-70f, 26f); lr.anchoredPosition = new Vector2(62f, -4f);
        percent = PixelUIKit.CreateText(clicker.UIFont, p.transform, "Percent", "", 20f, TextAlignmentOptions.MidlineRight, FontStyles.Bold, Color.white);
        percent.raycastTarget = false;
        RectTransform pr = percent.rectTransform;
        pr.anchorMin = new Vector2(0f, 1f); pr.anchorMax = new Vector2(1f, 1f); pr.pivot = new Vector2(1f, 1f);
        pr.sizeDelta = new Vector2(-70f, 26f); pr.anchoredPosition = new Vector2(-12f, -4f);

        GameObject track = new GameObject("Track", typeof(RectTransform), typeof(Image));
        track.transform.SetParent(p.transform, false);
        track.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        track.GetComponent<Image>().raycastTarget = false;
        RectTransform tr = track.GetComponent<RectTransform>();
        tr.anchorMin = new Vector2(0f, 0f); tr.anchorMax = new Vector2(1f, 0f); tr.pivot = new Vector2(0f, 0f);
        tr.sizeDelta = new Vector2(-76f, 12f); tr.anchoredPosition = new Vector2(62f, 8f);

        GameObject f = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        f.transform.SetParent(track.transform, false);
        Image fi = f.GetComponent<Image>();
        fi.color = new Color(1f, 0.82f, 0.25f, 1f);
        fi.raycastTarget = false;
        fill = f.GetComponent<RectTransform>();
        fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0f, 1f); fill.pivot = new Vector2(0f, 0.5f);
        fill.offsetMin = fill.offsetMax = Vector2.zero;
        built = true;
    }
}
