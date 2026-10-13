// ---- System notes ----
// PixelBlockHold.cs
//   Block Hold (shop pack with unlocksBlockHold, Upgrades tab, cheap while testing; component added by PixelShop / PixelClicker.Awake, Activate / Deactivate like the Bank):
//   HOLD the RIGHT mouse button on the block on the cube to pull it out of the rotation (PixelClicker.TryPullLive: the cube rolls a new block at once, so you can keep clicking
//   while it stays held). Letting go puts it back IN LINE (PixelClicker.ReturnHeldTier: it comes AFTER the block that is on the cube now, with the hits it had - RollTier hands it out - so a hazard that came up while you held is dealt with first and holding / releasing can never reroll the cube). The hold drains a meter (holdSeconds); letting go starts a short cooldown (releaseCooldownSeconds) before the meter recharges
//   (rechargeSeconds for empty -> full). If the meter runs dry while holding, the block is put back anyway, the meter flashes red and the cooldown is longer
//   (exhaustCooldownSeconds). Can't be held: hazards, Bottle / Whale / Fish / Treasure, or while a fish is being reeled in. A block with hits CAN be held and keeps them.
//   Releasing a held block over a hazard moves the hazard to next in line (it is not removed). Filler can be held. The look (beam, aura, side slide, cursor arc meter) is described at the end of these notes. METER LOOK: the arc fill glides to the real value, goes blue -> amber -> red as it runs low (flickering faster the lower it gets), has a soft glow behind it, a bright pulsing tip riding the end of the fill, quarter-mark dots and a white "ready again" pop when a recharge finishes. The hold lasts 5 seconds by default, 10 for a hold block (PixelBlockRules: Whetstone, Pesticide...).
//   HOLD BLOCKS: what a held block gives is data (PixelBlockRules.HoldRule: rapid extra clicks / payout / XP multiplier + a label shown beside the meter); Whetstone (Woodcutting, 3 hits) = 2 extra rapid clicks (x3 at once).
//   A partly broken block keeps its hits through the hold (TryPullLive -> ReturnHeldTier -> pendingHits), so a whetstone you only noticed after a click or two can still be grabbed.
//   A right press within rightSpamGapSeconds (0.45) of another right press or of a hazard right click never starts a hold (spamming a hazard away must not grab the next block).
//   STALL: PixelTier.stallsHold / stallSeconds (Great Oak): every click on such a block freezes the drain for stallSeconds and adds stallRefill (0.04) of the meter (PixelBlockHold.Stall), the meter turns icy while frozen.
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
    [Tooltip("Seconds the right button can hold a HOLD BLOCK (one with a bonus, see PixelBlockRules) with a full meter. 0 = the default (10).")]
    [SerializeField] private float holdBlockSeconds = 0f;
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
    [Tooltip("Colour of the meter, the beam and the aura around the held block.")]
    [SerializeField] private Color meterColor = new Color(0.45f, 0.85f, 1f, 1f);
    [Min(0f)]
    [Tooltip("Seconds the held block takes to slide out to the side / back in. 0 = the default (0.25).")]
    [SerializeField] private float slideSeconds = 0f;
    [Min(0f)]
    [Tooltip("How far to the side of the cube the held block hovers, in cube widths. 0 = the default (2.3).")]
    [SerializeField] private float sideDistance = 0f;
    [Tooltip("Colour the meter flashes when it ran dry.")]
    [SerializeField] private Color exhaustColor = new Color(1f, 0.2f, 0.15f, 1f);
    [Min(0f)]
    [Tooltip("A right press that comes this soon after another right press (or a right click on a hazard) is part of a spam, not a hold: it does not grab the block. 0 = the default (0.45).")]
    [SerializeField] private float rightSpamGapSeconds = 0f;
    [Tooltip("Switch the whole feature off (even when the upgrade is bought).")]
    [SerializeField] private bool disableBlockHold = false;

    public static PixelBlockHold Instance { get; private set; }
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; dotSprite = null; arcSprite = null; }

    private float HoldSeconds => holdSeconds > 0f ? holdSeconds : 5f;
    private float RechargeSeconds => rechargeSeconds > 0f ? rechargeSeconds : 8f;
    private float ReleaseCooldown => releaseCooldownSeconds > 0f ? releaseCooldownSeconds : 1f;
    private float ExhaustCooldown => exhaustCooldownSeconds > 0f ? exhaustCooldownSeconds : 3f;
    private float FlashSeconds => flashSeconds > 0f ? flashSeconds : 1.5f;
    private float HoldBlockSeconds => holdBlockSeconds > 0f ? holdBlockSeconds : 10f;
    private float SpamGap => rightSpamGapSeconds > 0f ? rightSpamGapSeconds : 0.45f;
    private float lastRightPress = -99f;
    private float MinStart => minStartFraction > 0f ? minStartFraction : 0.1f;

    private PixelClicker clicker;
    private bool unlocked, holding;
    private int heldTier = -1, heldHits;
    private float meter = 1f, cooldown, flash;
    private float stallUntil;   // while a click on a stalling block is fresh the hold meter does not drain
    private float shown = 1f, readyPop;   // the meter as drawn (it glides to the real value) / the "recharged!" pop timer

    // ---- state of the held block's picture ----
    private GameObject holder, aura;
    private Material auraMaterial;
    private LineRenderer[] beams;
    private Material beamMaterial;
    private float slide;            // 0 = at the cube, 1 = hovering at the side
    private bool returning;         // sliding back in after the button was let go

    // ---- the meter next to the cursor ----
    private GameObject canvasRoot;
    private RectTransform panel, canvasRect;
    private Image fill, back;
    private TMPro.TMP_Text bonusLabel;
    private Image glow, ready, tip;
    private readonly Image[] ticks = new Image[3];
    private bool wasFull = true;
    private static Sprite dotSprite;
    private static Sprite arcSprite;
    private const float ArcHalfDegrees = 55f;

    public bool Active => unlocked && !disableBlockHold;
    public bool Holding => holding;

    /// <summary>A click landed on a stalling block (PixelTier.stallsHold): the hold meter stops draining for this many seconds.</summary>
    public static void Stall(float seconds, float refill = 0f)
    {
        PixelBlockHold h = Instance;
        if (h == null) return;
        h.stallUntil = Mathf.Max(h.stallUntil, Time.unscaledTime + Mathf.Max(0.05f, seconds));
        if (refill > 0f && h.Active)
        {
            h.meter = Mathf.Min(1f, h.meter + refill);   // the click also tops the meter up (while holding or recharging)
            if (!h.holding) h.cooldown = 0f;              // ...and a waiting recharge starts at once
        }
    }

    /// <summary>Takes this fraction of the meter's CURRENT charge away (a hazard went off). The drawn meter glides down to it.</summary>
    public void LoseFraction(float fraction)
    {
        if (!Active) return;
        meter *= 1f - Mathf.Clamp01(fraction);
        flash = Mathf.Max(flash, 0.6f);   // a short red flash on the meter
        if (holding && meter < MinStart * 0.2f) { /* the normal drain will end the hold when it reaches empty */ }
    }

    private bool Stalled => Time.unscaledTime < stallUntil;

    /// <summary>The tier index of the block in hand (-1 when nothing is held).</summary>
    /// <summary>Is the block in hand a hold block (one with a bonus rule)? It can be held twice as long.</summary>
    private bool HeldIsHoldBlock()
    {
        return TryGetHeldType(out PixelClicker.PixelType t) && PixelBlockRules.TryGetHold(t, out PixelBlockRules.HoldRule _);
    }

    public int HeldTierIndex => holding ? heldTier : -1;

    /// <summary>The type of the block in hand (false when nothing is held).</summary>
    public bool TryGetHeldType(out PixelClicker.PixelType type)
    {
        type = PixelClicker.PixelType.White;
        if (!holding || clicker == null || !clicker.IsValidTierIndex(heldTier)) return false;
        type = clicker.Tiers[heldTier].type;
        return true;
    }

    private float SlideSeconds => slideSeconds > 0f ? slideSeconds : 0.25f;
    private float SideDistance => sideDistance > 0f ? sideDistance : 2.3f;

    private void Awake() { Instance = this; clicker = GetComponent<PixelClicker>(); }

    /// <summary>The shop upgrade was bought.</summary>
    public void Activate() { unlocked = true; }

    /// <summary>The upgrade is not owned.</summary>
    public void Deactivate()
    {
        if (holding) Release(false);
        FinishReturn();
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
            FinishReturn();   // (no slide while paused: the block goes straight back)
            return;
        }

        float dt = Time.unscaledDeltaTime;
        if (holding)
        {
            if (!PixelInput.RightHeld()) Release(false);
            else
            {
                if (!Stalled) meter -= dt / (HeldIsHoldBlock() ? HoldBlockSeconds : HoldSeconds);   // (a stalling block, e.g. the Great Oak, freezes it)
                if (meter <= 0f) { meter = 0f; Release(true); }
            }
        }
        else
        {
            if (cooldown > 0f) cooldown -= dt;
            else meter = Mathf.Min(1f, meter + dt / RechargeSeconds);
            if (PixelInput.RightPressed())
            {
                // Right-clicking a hazard away means spamming the button; the press that lands on the NEXT block must not grab it (that costs the meter and breaks the flow).
                bool spam = Time.unscaledTime - lastRightPress < SpamGap || Time.unscaledTime - clicker.LastDefuseTime < SpamGap;
                lastRightPress = Time.unscaledTime;
                if (!spam && CanStart()) Begin();
            }
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
        if (!clicker.TryPullLive(out heldTier, out heldHits)) return;
        holding = true;
        returning = false;
        slide = 0f;
        BuildVisual();
        PixelAudio.Play("ui_click");
    }

    private void Release(bool exhausted)
    {
        holding = false;
        // The block goes first in line (it comes after the block on the cube now); the picture of it only slides back for show.
        if (heldTier >= 0 && clicker != null) clicker.ReturnHeldTier(heldTier, heldHits);
        heldTier = -1;
        returning = holder != null;
        cooldown = exhausted ? ExhaustCooldown : ReleaseCooldown;
        if (exhausted) flash = FlashSeconds;
        if (!returning) DestroyVisual();
        PixelAudio.Play("click");
    }

    /// <summary>The slide back is over (or skipped): the picture of the held block goes away.</summary>
    private void FinishReturn()
    {
        returning = false;
        DestroyVisual();
    }

    private void LateUpdate()
    {
        if (clicker == null) return;
        UpdateVisual();
        UpdateMeter();
    }

    // ------------------------------------------------------------------
    // The telekinetic beam
    // ------------------------------------------------------------------

    private void BuildVisual()
    {
        DestroyVisual();
        float size = clicker.PixelBaseSize;
        holder = new GameObject("Held Block");
        holder.transform.position = clicker.LiveWorldPosition;
        GameObject model = clicker.CreateDisplayPixel(heldTier, holder.transform, size);
        if (model != null) model.transform.localPosition = Vector3.zero;

        // The aura: a pulsing translucent shell round the block.
        PixelMold.MakeShell(holder.transform, size * 1.35f, out auraMaterial);
        aura = holder.transform.GetChild(holder.transform.childCount - 1).gameObject;

        // The beam: three wobbling lines from the cube to the block.
        beamMaterial = new Material(PixelShaders.SpriteDefault());
        beams = new LineRenderer[3];
        for (int i = 0; i < beams.Length; i++)
        {
            GameObject g = new GameObject("Beam " + i);
            g.transform.SetParent(holder.transform, false);
            LineRenderer lr = g.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 12;
            lr.sharedMaterial = beamMaterial;
            lr.startWidth = lr.endWidth = size * (i == 0 ? 0.09f : 0.04f);
            lr.numCapVertices = 3;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            beams[i] = lr;
        }
    }

    private void DestroyVisual()
    {
        if (holder != null) Destroy(holder);
        if (auraMaterial != null) Destroy(auraMaterial);
        if (beamMaterial != null) Destroy(beamMaterial);
        holder = null; aura = null; auraMaterial = null; beamMaterial = null; beams = null;
    }

    private void UpdateVisual()
    {
        if (holder == null) return;
        float dt = Time.unscaledDeltaTime;
        slide = Mathf.Clamp01(slide + (returning ? -dt : dt) / SlideSeconds);
        if (returning && slide <= 0f) { FinishReturn(); return; }

        float size = clicker.PixelBaseSize;
        Camera cam = clicker.TargetCamera;
        Vector3 right = cam != null ? cam.transform.right : Vector3.right;
        Vector3 up = cam != null ? cam.transform.up : Vector3.up;
        Vector3 cubePos = clicker.LiveWorldPosition;
        float eased = slide * slide * (3f - 2f * slide);
        float time = Time.unscaledTime;
        Vector3 hover = cubePos + right * size * SideDistance + up * size * 0.35f;
        Vector3 pos = Vector3.Lerp(cubePos, hover, eased) + up * (Mathf.Sin(time * 2.2f) * 0.07f * size * eased);
        holder.transform.position = pos;
        holder.transform.Rotate(up, 35f * dt, Space.World);

        bool flashing = flash > 0f;
        Color c = Color.Lerp(meterColor, exhaustColor, flashing ? 0.5f + 0.5f * Mathf.Sin(time * 22f) : 0f);
        float pulse = 0.75f + 0.25f * Mathf.Sin(time * 5f);
        if (aura != null)
        {
            aura.transform.localScale = Vector3.one * size * 1.35f * (1f + 0.05f * Mathf.Sin(time * 6f));
            Color ac = c; ac.a = 0.34f * pulse * Mathf.Clamp01(eased * 1.6f + 0.25f);
            auraMaterial.color = ac;
        }
        if (beams != null)
        {
            Vector3 dir = pos - cubePos;
            Vector3 perp = cam != null ? Vector3.Cross(dir, cam.transform.forward).normalized : Vector3.up;
            float amp = 0.12f * size;
            for (int b = 0; b < beams.Length; b++)
            {
                LineRenderer lr = beams[b];
                lr.enabled = eased > 0.03f;
                Color bc = c; bc.a = (b == 0 ? 0.85f : 0.55f) * pulse;
                lr.startColor = bc; bc.a *= 0.4f; lr.endColor = bc;
                for (int i = 0; i < lr.positionCount; i++)
                {
                    float t = i / (float)(lr.positionCount - 1);
                    float wob = b == 0 ? 0.25f : 1f;
                    float off = Mathf.Sin(t * Mathf.PI) * amp * wob * Mathf.Sin(time * (8f + b * 3f) + t * (7f + b * 4f) + b * 2.1f);
                    lr.SetPosition(i, Vector3.Lerp(cubePos, pos, t) + perp * off);
                }
            }
        }
    }

    // ------------------------------------------------------------------
    // The meter: a curved vertical arc to the LEFT of the mouse cursor (tooltips pop up on the right)
    // ------------------------------------------------------------------

    private static Sprite BuildArcSprite()
    {
        const int n = 192;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        Color32[] px = new Color32[n * n];
        float c = (n - 1) * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x - c) / c, dy = (y - c) / c;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;   // 0 = east
                float radial = Mathf.Clamp01(Mathf.Min((r - 0.78f) / 0.03f, (0.98f - r) / 0.03f));
                float angular = Mathf.Clamp01((ArcHalfDegrees - Mathf.Abs(ang)) / 2.5f);
                px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * radial * angular));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }

    /// <summary>A soft white dot (bright centre, fading edge): the glowing tip and the quarter marks.</summary>
    private static Sprite BuildDotSprite()
    {
        const int n = 64;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        Color32[] px = new Color32[n * n];
        float c = (n - 1) * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a);
                px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * a));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }

    private void UpdateMeter()
    {
        bool full = meter >= 0.999f;
        bool show = Active && !PixelTitleScreen.Showing && (holding || returning || flash > 0f || cooldown > 0f || !full || readyPop > 0f);
        if (!show) { if (canvasRoot != null && canvasRoot.activeSelf) canvasRoot.SetActive(false); wasFull = true; return; }
        if (canvasRoot == null) BuildMeter();
        if (canvasRoot == null) return;
        canvasRoot.SetActive(true);
        float dt = Time.unscaledDeltaTime;
        float time = Time.unscaledTime;

        // The "ready again" pop: the moment a recharge finishes.
        if (full && !wasFull && !holding) readyPop = 0.45f;
        wasFull = full;
        if (readyPop > 0f) readyPop -= dt;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, PixelInput.PointerPosition(), null, out Vector2 local))
            panel.anchoredPosition = local + new Vector2(-6f, 0f);

        PixelBlockRules.HoldRule bonus = holding ? PixelBlockRules.ActiveHold : null;
        if (bonusLabel != null)
        {
            bool on = bonus != null && !string.IsNullOrEmpty(bonus.label);
            if (bonusLabel.gameObject.activeSelf != on) bonusLabel.gameObject.SetActive(on);
            if (on)
            {
                if (bonusLabel.text != bonus.label) bonusLabel.text = bonus.label;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, PixelInput.PointerPosition(), null, out Vector2 lp2))
                    bonusLabel.rectTransform.anchoredPosition = lp2 + new Vector2(-120f, -118f);
            }
        }

        // The drawn fill glides to the real value (smooth drain and recharge).
        shown = Mathf.MoveTowards(shown, Mathf.Clamp01(meter), dt * 3.5f + Mathf.Abs(shown - meter) * 8f * dt);
        float k = Mathf.Clamp01(shown);
        float sweep = Mathf.Lerp(90f - ArcHalfDegrees, 90f + ArcHalfDegrees, k);   // degrees from the bottom (the arc sprite shows 35..145 degrees)
        fill.fillAmount = sweep / 360f;
        if (glow != null) glow.fillAmount = fill.fillAmount;

        bool flashing = flash > 0f;
        float pulse = flashing ? 0.5f + 0.5f * Mathf.Sin(time * 22f) : 0f;
        // Colour: cool blue when plenty is left, amber then red as it runs out (while holding), a calm dim blue while recharging.
        Color c = meterColor;
        if (holding) c = k > 0.5f ? meterColor : Color.Lerp(new Color(1f, 0.55f, 0.15f, 1f), meterColor, (k - 0.25f) / 0.25f);
        if (holding && k <= 0.25f) c = Color.Lerp(exhaustColor, new Color(1f, 0.55f, 0.15f, 1f), k / 0.25f);
        float warn = holding && k < 0.3f ? 0.5f + 0.5f * Mathf.Sin(time * (14f + (0.3f - k) * 40f)) : 0f;   // faster flicker the lower it gets
        c = Color.Lerp(c, Color.white, warn * 0.35f);
        c = Color.Lerp(c, exhaustColor, flashing ? 0.4f + 0.6f * pulse : 0f);
        if (holding && Stalled) c = Color.Lerp(c, new Color(0.75f, 0.95f, 1f, 1f), 0.65f);   // frozen: an icy white-blue
        if (!holding && !flashing) c = Color.Lerp(c, new Color(0.35f, 0.5f, 0.75f, 1f), 0.45f);   // recharging: subdued
        if (!holding && cooldown > 0f && !flashing) c.a = 0.6f;   // waiting for the recharge to begin
        fill.color = c;
        back.color = flashing ? Color.Lerp(new Color(0f, 0f, 0f, 0.6f), new Color(0.55f, 0f, 0f, 0.85f), pulse) : new Color(0f, 0f, 0f, 0.6f);

        // The soft glow behind the fill, pulsing while held.
        if (glow != null)
        {
            Color gc = c; gc.a = holding ? 0.28f + 0.14f * Mathf.Sin(time * 6f) : 0.12f;
            glow.color = gc;
        }

        // The bright tip at the end of the fill: it rides the arc as the meter moves.
        if (tip != null)
        {
            float angle = (-90f + sweep) * Mathf.Deg2Rad;
            float radius = panel.sizeDelta.x * 0.5f * 0.88f;
            tip.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            float tipScale = (holding ? 1f + 0.18f * Mathf.Sin(time * 9f) : 0.8f) * (1f + (readyPop > 0f ? readyPop * 0.8f : 0f));
            tip.rectTransform.localScale = Vector3.one * tipScale;
            Color tc = Color.Lerp(c, Color.white, 0.6f); tc.a = k < 0.01f ? 0f : 0.95f;
            tip.color = tc;
        }

        // Quarter marks along the track, lit up to the current level.
        for (int i = 0; i < ticks.Length; i++)
            if (ticks[i] != null) { Color tcol = k >= (i + 1) * 0.25f - 0.01f ? new Color(1f, 1f, 1f, 0.55f) : new Color(1f, 1f, 1f, 0.18f); ticks[i].color = tcol; }

        // "Ready!" pop when it is full again: a bright arc that swells and fades.
        if (ready != null)
        {
            float rp = Mathf.Clamp01(readyPop / 0.45f);
            Color rc = Color.white; rc.a = rp * 0.8f;
            ready.color = rc;
            ready.rectTransform.sizeDelta = panel.sizeDelta * (1.06f + (1f - rp) * 0.18f);
            ready.fillAmount = 1f;
        }
    }

    private void BuildMeter()
    {
        canvasRoot = PixelUIKit.CreateCanvas("Block Hold", 55, new Vector2(1920f, 1080f), false);
        canvasRect = canvasRoot.GetComponent<RectTransform>();
        if (arcSprite == null) arcSprite = BuildArcSprite();

        GameObject p = new GameObject("Meter", typeof(RectTransform));
        p.transform.SetParent(canvasRoot.transform, false);
        panel = p.GetComponent<RectTransform>();
        panel.sizeDelta = new Vector2(190f, 190f);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        panel.localScale = new Vector3(-1f, 1f, 1f);   // the arc sprite is on the right: mirrored it sits on the LEFT of the cursor (the block tooltips pop up on the right)

        Image Arc(string name, float scale)
        {
            GameObject g = new GameObject(name, typeof(RectTransform), typeof(Image));
            g.transform.SetParent(panel, false);
            Image im = g.GetComponent<Image>();
            im.sprite = arcSprite;
            im.raycastTarget = false;
            RectTransform r = g.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = panel.sizeDelta * scale;
            r.anchoredPosition = Vector2.zero;
            return im;
        }
        Image Dot(string name, Vector2 pos, float size)
        {
            GameObject g = new GameObject(name, typeof(RectTransform), typeof(Image));
            g.transform.SetParent(panel, false);
            Image im = g.GetComponent<Image>();
            im.sprite = dotSprite;
            im.raycastTarget = false;
            RectTransform r = g.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(size, size);
            r.anchoredPosition = pos;
            return im;
        }
        back = Arc("Back", 1.06f);
        glow = Arc("Glow", 1.12f);
        fill = Arc("Fill", 1f);
        ready = Arc("Ready", 1.06f);
        foreach (Image f in new[] { glow, fill })
        {
            f.type = Image.Type.Filled;
            f.fillMethod = Image.FillMethod.Radial360;
            f.fillOrigin = (int)Image.Origin360.Bottom;
            f.fillClockwise = false;   // bottom -> right -> top
        }
        ready.color = new Color(1f, 1f, 1f, 0f);
        ready.type = Image.Type.Filled; ready.fillMethod = Image.FillMethod.Radial360; ready.fillOrigin = (int)Image.Origin360.Bottom; ready.fillClockwise = false;
        ready.fillAmount = 1f;
        // Quarter marks: small dots on the track at 25 / 50 / 75 %.
        if (dotSprite == null) dotSprite = BuildDotSprite();
        float trackR = panel.sizeDelta.x * 0.5f * 0.88f;
        for (int i = 0; i < ticks.Length; i++)
        {
            float a = (-90f + Mathf.Lerp(90f - ArcHalfDegrees, 90f + ArcHalfDegrees, (i + 1) * 0.25f)) * Mathf.Deg2Rad;
            ticks[i] = Dot("Tick " + (i + 1), new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * trackR, 9f);
        }
        tip = Dot("Tip", Vector2.zero, 34f);

        // The bonus text of a held bonus block (PixelBlockRules.HoldRule.label), under the arc.
        bonusLabel = PixelUIKit.CreateText(clicker.UIFont, canvasRoot.transform, "Bonus Label", "", 30f, TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold, new Color(1f, 0.9f, 0.4f, 1f));
        bonusLabel.raycastTarget = false;
        RectTransform blr = bonusLabel.rectTransform;
        blr.anchorMin = blr.anchorMax = blr.pivot = new Vector2(0.5f, 0.5f);
        blr.sizeDelta = new Vector2(320f, 44f);
        bonusLabel.gameObject.SetActive(false);
    }
}
