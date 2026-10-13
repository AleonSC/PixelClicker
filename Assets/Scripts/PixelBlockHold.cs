// ---- System notes ----
// PixelBlockHold.cs
//   Block Hold (shop pack with unlocksBlockHold, Upgrades tab, cheap while testing; component added by PixelShop / PixelClicker.Awake, Activate / Deactivate like the Bank):
//   HOLD the RIGHT mouse button on the block on the cube to pull it out of the rotation (PixelClicker.TryPullLive: the cube rolls a new block at once, so you can keep clicking
//   while it stays held). Letting go puts it back (PixelClicker.ReturnHeldTier: it takes the cube at once, with the hits it had; the block that was on the cube moves to next in line, hits kept - RollTier hands it out). The hold drains a meter (holdSeconds); letting go starts a short cooldown (releaseCooldownSeconds) before the meter recharges
//   (rechargeSeconds for empty -> full). If the meter runs dry while holding, the block is put back anyway, the meter flashes red and the cooldown is longer
//   (exhaustCooldownSeconds). Can't be held: hazards, Bottle / Whale / Fish / Treasure, or while a fish is being reeled in. A block with hits CAN be held and keeps them.
//   Releasing a held block over a hazard moves the hazard to next in line (it is not removed). Filler can be held. The look (beam, aura, side slide, cursor arc meter) is described at the end of these notes.
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
    private int heldTier = -1, heldHits;
    private float meter = 1f, cooldown, flash;

    // ---- state of the held block's picture ----
    private GameObject holder, aura;
    private Material auraMaterial;
    private LineRenderer[] beams;
    private Material beamMaterial;
    private float slide;            // 0 = at the cube, 1 = hovering at the side
    private bool returning;         // sliding back in after the button was let go
    private int returnTier = -1, returnHits;

    // ---- the meter next to the cursor ----
    private GameObject canvasRoot;
    private RectTransform panel, canvasRect;
    private Image fill, back;
    private static Sprite arcSprite;
    private const float ArcHalfDegrees = 55f;

    public bool Active => unlocked && !disableBlockHold;
    public bool Holding => holding;
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
                meter -= dt / HoldSeconds;
                if (meter <= 0f) { meter = 0f; Release(true); }
            }
        }
        else
        {
            if (cooldown > 0f) cooldown -= dt;
            else meter = Mathf.Min(1f, meter + dt / RechargeSeconds);
            if (!returning && PixelInput.RightPressed() && CanStart()) Begin();
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
        slide = 0f;
        BuildVisual();
        PixelAudio.Play("ui_click");
    }

    private void Release(bool exhausted)
    {
        holding = false;
        returning = holder != null;   // the block slides back in, then is put back
        returnTier = heldTier;
        returnHits = heldHits;
        heldTier = -1;
        cooldown = exhausted ? ExhaustCooldown : ReleaseCooldown;
        if (exhausted) flash = FlashSeconds;
        if (!returning) FinishReturn();
        PixelAudio.Play("click");
    }

    /// <summary>The slide back is over (or skipped): the block goes back into the rotation and its picture goes away.</summary>
    private void FinishReturn()
    {
        if (returnTier >= 0 && clicker != null) clicker.ReturnHeldTier(returnTier, returnHits);
        returnTier = -1;
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

    private void UpdateMeter()
    {
        bool show = Active && !PixelTitleScreen.Showing && (holding || returning || flash > 0f || cooldown > 0f || meter < 0.999f);
        if (!show) { if (canvasRoot != null && canvasRoot.activeSelf) canvasRoot.SetActive(false); return; }
        if (canvasRoot == null) BuildMeter();
        if (canvasRoot == null) return;
        canvasRoot.SetActive(true);

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, PixelInput.PointerPosition(), null, out Vector2 local))
            panel.anchoredPosition = local + new Vector2(-6f, 0f);

        float k = Mathf.Clamp01(meter);
        // (the arc sprite shows 35..145 degrees of the circle, measured from the bottom, going round the right side)
        fill.fillAmount = Mathf.Lerp(90f - ArcHalfDegrees, 90f + ArcHalfDegrees, k) / 360f;
        bool flashing = flash > 0f;
        float pulse = flashing ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 22f) : 0f;
        Color c = Color.Lerp(meterColor, exhaustColor, flashing ? 0.4f + 0.6f * pulse : 0f);
        if (!holding && cooldown > 0f && !flashing) c.a = 0.6f;   // waiting for the recharge to begin
        fill.color = c;
        back.color = flashing ? Color.Lerp(new Color(0f, 0f, 0f, 0.6f), new Color(0.55f, 0f, 0f, 0.85f), pulse) : new Color(0f, 0f, 0f, 0.6f);
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
        back = Arc("Back", 1.06f);
        fill = Arc("Fill", 1f);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Radial360;
        fill.fillOrigin = (int)Image.Origin360.Bottom;
        fill.fillClockwise = false;   // bottom -> right -> top
    }
}
