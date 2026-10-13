// ---- System notes ----
// PixelBlockHold.cs
//   Block Hold (shop pack with unlocksBlockHold, Upgrades tab, cheap while testing; component added by PixelShop / PixelClicker.Awake, Activate / Deactivate like the Bank):
//   HOLD the RIGHT mouse button on the block on the cube to pull it out of the rotation (PixelClicker.TryPullLive: the cube rolls a new block at once, so you can keep clicking
//   while it stays held). Letting go puts it back (PixelClicker.ReturnHeldTier: it replaces the block on the cube if that one has no hits yet, else it comes after that block
//   breaks - RollTier hands out the queued tier). The hold drains a meter (holdSeconds); letting go starts a short cooldown (releaseCooldownSeconds) before the meter recharges
//   (rechargeSeconds for empty -> full). If the meter runs dry while holding, the block is put back anyway, the meter flashes red and the cooldown is longer
//   (exhaustCooldownSeconds). Can't be held: hazards, filler blocks, Bottle / Whale / Fish / Treasure, a block that already has hits, or while a fish is being reeled in.
//   Releasing a held block over a fresh hazard replaces the hazard (a deliberate dodge, it costs the meter). A small meter bar (+ the held block's name) sits under the cube.
//   Not saved (the meter starts full); the pack itself is saved like any pack. No Toggles switch (right click does nothing else on normal blocks).
// ---------------------------------------------------
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PixelBlockHold : MonoBehaviour
{
    [Header("Meter")]
    [Min(0f)]
    [Tooltip("Seconds the right button can hold a block with a full meter. 0 = the default (5).")]
    [SerializeField] private float holdSeconds = 0f;
    [Min(0f)]
    [Tooltip("Seconds the meter takes to recharge from empty to full. 0 = the default (8).")]
    [SerializeField] private float rechargeSeconds = 0f;
    [Min(0f)]
    [Tooltip("Seconds after you let go before the meter starts to recharge. 0 = the default (1).")]
    [SerializeField] private float releaseCooldownSeconds = 0f;
    [Min(0f)]
    [Tooltip("Seconds before recharging when the meter ran dry while holding (a longer wait). 0 = the default (3).")]
    [SerializeField] private float exhaustCooldownSeconds = 0f;
    [Min(0f)]
    [Tooltip("Seconds the meter flashes red after running dry. 0 = the default (1.5).")]
    [SerializeField] private float flashSeconds = 0f;
    [Range(0f, 1f)]
    [Tooltip("A hold can only start when the meter is at least this full. 0 = the default (0.1).")]
    [SerializeField] private float minStartFraction = 0f;

    [Header("Look")]
    [Tooltip("Colour of the meter while holding / recharging.")]
    [SerializeField] private Color meterColor = new Color(0.45f, 0.85f, 1f, 1f);
    [Tooltip("Colour the meter flashes when it ran dry.")]
    [SerializeField] private Color exhaustColor = new Color(1f, 0.2f, 0.15f, 1f);
    [Tooltip("Switch the whole feature off (even when the upgrade is bought).")]
    [SerializeField] private bool disableBlockHold = false;

    public static PixelBlockHold Instance { get; private set; }
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; }

    private float HoldSeconds => holdSeconds > 0f ? holdSeconds : 5f;
    private float RechargeSeconds => rechargeSeconds > 0f ? rechargeSeconds : 8f;
    private float ReleaseCooldown => releaseCooldownSeconds > 0f ? releaseCooldownSeconds : 1f;
    private float ExhaustCooldown => exhaustCooldownSeconds > 0f ? exhaustCooldownSeconds : 3f;
    private float FlashSeconds => flashSeconds > 0f ? flashSeconds : 1.5f;
    private float MinStart => minStartFraction > 0f ? minStartFraction : 0.1f;

    private PixelClicker clicker;
    private bool unlocked, holding;
    private int heldTier = -1;
    private float meter = 1f, cooldown, flash;

    // UI
    private GameObject canvasRoot;
    private RectTransform panel, canvasRect;
    private Image fill, back;
    private TMP_Text label;

    public bool Active => unlocked && !disableBlockHold;
    public bool Holding => holding;

    private void Awake() { Instance = this; clicker = GetComponent<PixelClicker>(); }

    /// <summary>The shop upgrade was bought.</summary>
    public void Activate() { unlocked = true; }

    /// <summary>The upgrade is not owned.</summary>
    public void Deactivate()
    {
        if (holding) Release(false);
        unlocked = false;
        if (canvasRoot != null) canvasRoot.SetActive(false);
    }

    private void Update()
    {
        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
        if (!Active || clicker == null) { if (canvasRoot != null && canvasRoot.activeSelf) canvasRoot.SetActive(false); return; }
        if (PixelTitleScreen.Showing || PixelPauseMenu.IsPaused)
        {
            if (holding) Release(false);
            return;
        }

        float dt = Time.unscaledDeltaTime;
        if (holding)
        {
            if (!PixelInput.RightHeld()) Release(false);
            else
            {
                meter -= dt / HoldSeconds;
                if (meter <= 0f) { meter = 0f; Release(true); }
            }
        }
        else
        {
            if (cooldown > 0f) cooldown -= dt;
            else meter = Mathf.Min(1f, meter + dt / RechargeSeconds);
            if (PixelInput.RightPressed() && CanStart()) Begin();
        }
        if (flash > 0f) flash -= dt;
    }

    private bool CanStart()
    {
        if (meter < MinStart || PixelInput.PointerOverUI() || PixelClicker.ExternalClickBlock) return false;
        return clicker.TryGetBlockAt(PixelInput.PointerPosition(), out int _, out bool live, out Rigidbody _) && live;
    }

    private void Begin()
    {
        if (!clicker.TryPullLive(out heldTier)) return;
        holding = true;
        PixelAudio.Play("ui_click");
    }

    private void Release(bool exhausted)
    {
        holding = false;
        clicker.ReturnHeldTier(heldTier);
        heldTier = -1;
        cooldown = exhausted ? ExhaustCooldown : ReleaseCooldown;
        if (exhausted) flash = FlashSeconds;
        PixelAudio.Play("click");
    }

    private void LateUpdate()
    {
        if (!Active || clicker == null || PixelTitleScreen.Showing) return;
        bool show = holding || flash > 0f || cooldown > 0f || meter < 0.999f;
        if (!show) { if (canvasRoot != null && canvasRoot.activeSelf) canvasRoot.SetActive(false); return; }
        if (canvasRoot == null) BuildUi();
        if (canvasRoot == null) return;
        canvasRoot.SetActive(true);

        // Under the cube.
        Camera cam = clicker.TargetCamera;
        Vector3 world = clicker.LiveWorldPosition;
        if (cam != null)
        {
            Vector3 sp = cam.WorldToScreenPoint(world);
            Vector3 top = cam.WorldToScreenPoint(world + cam.transform.up * clicker.PixelBaseSize * 0.5f);
            float radius = Mathf.Abs(top.y - sp.y);
            if (sp.z > 0f && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, new Vector2(sp.x, sp.y - radius - 30f), null, out Vector2 local))
                panel.anchoredPosition = local;
        }
        float k = Mathf.Clamp01(meter);
        fill.fillAmount = k;
        bool flashing = flash > 0f;
        float pulse = flashing ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 22f) : 0f;
        Color c = Color.Lerp(meterColor, exhaustColor, flashing ? 0.4f + 0.6f * pulse : 0f);
        if (!holding && cooldown > 0f && !flashing) c.a = 0.55f;   // waiting for the recharge to begin
        fill.color = c;
        back.color = flashing ? Color.Lerp(new Color(0f, 0f, 0f, 0.55f), new Color(0.5f, 0f, 0f, 0.8f), pulse) : new Color(0f, 0f, 0f, 0.55f);
        label.text = holding && clicker.IsValidTierIndex(heldTier) ? "HOLDING " + clicker.Tiers[heldTier].displayName.ToUpperInvariant() : flashing ? "OUT OF ENERGY" : "";
    }

    private void BuildUi()
    {
        if (clicker.UIFont == null) return;
        canvasRoot = PixelUIKit.CreateCanvas("Block Hold", 55, new Vector2(1920f, 1080f), false);
        canvasRect = canvasRoot.GetComponent<RectTransform>();
        GameObject p = new GameObject("Meter", typeof(RectTransform));
        p.transform.SetParent(canvasRoot.transform, false);
        panel = p.GetComponent<RectTransform>();
        panel.sizeDelta = new Vector2(200f, 16f);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);

        GameObject b = new GameObject("Back", typeof(RectTransform), typeof(Image));
        b.transform.SetParent(panel, false);
        back = b.GetComponent<Image>();
        back.raycastTarget = false;
        PixelUIKit.Stretch(b.GetComponent<RectTransform>());

        GameObject f = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        f.transform.SetParent(panel, false);
        fill = f.GetComponent<Image>();
        fill.raycastTarget = false;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        RectTransform fr = f.GetComponent<RectTransform>();
        PixelUIKit.Stretch(fr);
        fr.offsetMin = new Vector2(2f, 2f);
        fr.offsetMax = new Vector2(-2f, -2f);

        label = PixelUIKit.CreateText(clicker.UIFont, panel, "Label", "", 20f, TextAlignmentOptions.Center, FontStyles.Bold, Color.white);
        label.raycastTarget = false;
        RectTransform lr = label.rectTransform;
        lr.anchorMin = new Vector2(0f, 1f);
        lr.anchorMax = new Vector2(1f, 1f);
        lr.pivot = new Vector2(0.5f, 0f);
        lr.sizeDelta = new Vector2(240f, 28f);
        lr.anchoredPosition = new Vector2(0f, 2f);
    }
}
