// ---- System notes ----
// PixelPowerClick.cs
//   POWER CLICK (TEST - may be removed or parked). Unlocked by the Standard skill tree node 'power' (requireTreeNode); branches 'quick' (charge 40%
//   faster) -> 'aftershock' (cooldown a third shorter). Hold the LEFT mouse button on the cube: the press is a normal
//   click as always, and if you keep holding for holdDelay (0.25 s) a charge builds (chargeSeconds 1.2): a ring fills round the cube, the cube
//   trembles harder and harder, and at full charge the ring turns gold and pulses. Let go at full charge and it fires: one click worth
//   clicksWorth (25) clicks (PixelClicker.PowerClick - payout x25, tough blocks take 25 hits, the combo grows by 25), a white flash, a shockwave
//   ring, a kick of the cube and a big 'POWER CLICK x25' popup with a boom. Letting go early just cancels (no cost). Then a cooldown
//   (cooldownSeconds 45) - a thin grey ring round the cube while you hover it shows what's left. Normal fast clicking never charges
//   (each press is shorter than holdDelay).
//   Not on: rare drops, hazards, Fish (Weak Spots) and the Golden Snitch; never while a husk is grabbed, a block is held (right mouse),
//   a takeover minigame runs, in first person, in a menu or while time is stopped. Switch: disablePowerClick. Stats: power.clicks.
//   RIGHT CLICK while charging (2026-10-18) cancels it and KEEPS the charge (savedCharge): the next charge starts at that level (no cooldown).
//   PixelRightClick gives that right press to the charge (Owner.Charge, read from PixelPowerClick.Charging), so Block Hold / hazard clearing ignore it.
//   Component in PixelSystems (group Core); its overlay canvas sits under the windows.
// ---------------------------------------------------
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PixelPowerClick : MonoBehaviour
{
    [Header("Power click (TEST)")]
    [Tooltip("Turn the power click off completely.")]
    [SerializeField] private bool disablePowerClick = false;
    [Tooltip("The power click is unlocked by the 'Power Click' node of the Standard skill tree. Off = available from the start (for testing).")]
    [SerializeField] private bool requireTreeNode = true;
    [Min(0.05f)]
    [Tooltip("Seconds you must keep holding after the press before the charge starts (so normal clicking never charges).")]
    [SerializeField] private float holdDelay = 0.25f;
    [Min(0.1f)]
    [Tooltip("Seconds of holding to fill the charge.")]
    [SerializeField] private float chargeSeconds = 1.2f;
    [Min(2)]
    [Tooltip("How many clicks one power click is worth (payout x this, tough blocks take this many hits, the combo grows by this).")]
    [SerializeField] private int clicksWorth = 25;
    [Min(0f)]
    [Tooltip("Seconds after a power click before the next one can charge.")]
    [SerializeField] private float cooldownSeconds = 45f;

    [Header("Look")]
    [Tooltip("Colour of the charge ring while it fills.")]
    [SerializeField] private Color chargeColor = new Color(1f, 1f, 1f, 0.9f);
    [Tooltip("Colour of the ring at full charge, the shockwave and the popup.")]
    [SerializeField] private Color readyColor = new Color(1f, 0.82f, 0.25f, 1f);
    [Tooltip("Colour of the cooldown ring shown while hovering the cube.")]
    [SerializeField] private Color cooldownColor = new Color(0.6f, 0.65f, 0.75f, 0.55f);
    [Range(0f, 1f)]
    [Tooltip("Strength of the white screen flash when it fires.")]
    [SerializeField] private float flashStrength = 0.55f;
    [Min(0f)]
    [Tooltip("How far the cube trembles at full charge, as a share of its size.")]
    [SerializeField] private float shakeAtFull = 0.06f;

    // Standard tree branches: 'quick' charges 40% faster, 'aftershock' cuts the cooldown by a third.
    private float ChargeSeconds => chargeSeconds * (PixelSkills.HasStandardNode("quick") ? 0.6f : 1f);
    private float CooldownSeconds => cooldownSeconds * (PixelSkills.HasStandardNode("aftershock") ? 2f / 3f : 1f);

    private PixelClicker clicker;
    private enum State { Idle, Pressed, Charging, Ready }
    private State state = State.Idle;
    private float pressTime, charge, cooldownLeft, fxTime;
    private float savedCharge;   // a charge cancelled with a right click (0..1): the next charge starts from it

    /// <summary>A power click is charging (or full) right now: a right press cancels it (PixelRightClick gives that press to it, Owner.Charge).</summary>
    public static bool Charging { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Charging = false; }
    private bool shaking;

    // UI
    private GameObject canvasObject;
    private RectTransform canvasRect, ringRect, cooldownRect, waveRect, popupRect;
    private PixelArcGraphic ring, cooldownRing, wave;
    private Image flash;
    private TMP_Text popup;
    private float flashT, waveT = 1f, popupT = 1f;
    private Vector2 waveCentre, popupStart;

    private void Awake()
    {
        clicker = GetComponent<PixelClicker>();
        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
    }

    private void OnDisable() { StopShake(); }

    private void Update()
    {
        if (cooldownLeft > 0f && !PixelCalm.InMenu) cooldownLeft -= Time.unscaledDeltaTime;
        if (disablePowerClick || clicker == null || (requireTreeNode && !PixelSkills.HasStandardNode("power"))) { Cancel(); UpdateFx(); return; }
        Camera cam = clicker.ViewCamera;
        if (cam == null) return;

        bool blocked = Blocked();
        Vector2 pointer = PixelInput.PointerPosition();

        switch (state)
        {
            case State.Idle:
                if (!blocked && cooldownLeft <= 0f && PixelInput.LeftPressed() && !PixelInput.PointerOverUI() && !PixelClicker.ExternalClickBlock && clicker.IsLiveCubeAt(pointer) && AllowedBlock())
                {
                    state = State.Pressed;
                    pressTime = Time.unscaledTime;
                }
                break;
            case State.Pressed:
                if (blocked || !PixelInput.LeftHeld()) { Cancel(); break; }
                if (Time.unscaledTime - pressTime >= holdDelay) { state = State.Charging; charge = savedCharge; savedCharge = 0f; PixelAudio.Play("overcharge", 0.6f); }   // (a charge cancelled with a right click carries on from where it stopped)
                break;
            case State.Charging:
            case State.Ready:
                if (blocked || !AllowedBlock()) { Cancel(); break; }
                if (PixelInput.RightPressed())
                {
                    // RIGHT CLICK = cancel, keeping the charge: the next power click starts at this level (no cooldown). PixelRightClick gives this
                    // press to the charge (Owner.Charge), so it never starts Block Hold or clears a hazard.
                    savedCharge = charge;
                    Cancel();
                    PixelAudio.Play("ui_click", 0.8f);
                    break;
                }
                if (!PixelInput.LeftHeld())
                {
                    if (state == State.Ready) Fire(cam);
                    else Cancel();   // let go early: nothing happens, no cooldown
                    break;
                }
                if (state == State.Charging)
                {
                    charge = Mathf.Clamp01(charge + Time.unscaledDeltaTime / ChargeSeconds);
                    if (charge >= 1f) { state = State.Ready; PixelAudio.Play("snitch_ready", 1.3f); }
                }
                Shake(state == State.Ready ? 1f : charge * charge);
                break;
        }

        Charging = state == State.Charging || state == State.Ready;
        EnsureUI();
        LayoutRings(cam, pointer);
        UpdateFx();
    }

    /// <summary>Things that stop a charge: menus, takeovers, first person, a grabbed husk, a held block, stopped time.</summary>
    private bool Blocked()
    {
        if (PixelCalm.InMenu || Time.timeScale <= 0f || PixelMinigame.TakeoverActive || PixelFirstPerson.Active) return true;
        PixelGrab grab = PixelFind.Cached<PixelGrab>();
        if (grab != null && grab.HeldBody != null) return true;
        if (PixelBlockHold.Instance != null && PixelBlockHold.Instance.Holding) return true;
        return false;
    }

    /// <summary>The block on the cube can take a power click (not a rare drop, hazard, Fish or the Golden Snitch).</summary>
    private bool AllowedBlock()
    {
        int i = clicker.CurrentClickTierIndex;
        if (!clicker.IsValidTierIndex(i)) return false;
        PixelClicker.PixelTier t = clicker.Tiers[i];
        return !t.rareDrop && !PixelClicker.IsHazard(t.type) && t.type != PixelClicker.PixelType.Fish && t.type != PixelClicker.PixelType.GoldenSnitch;
    }

    private void Cancel()
    {
        Charging = false;
        state = State.Idle;
        charge = 0f;
        StopShake();
    }

    private void Fire(Camera cam)
    {
        Charging = false;
        StopShake();
        state = State.Idle;
        charge = 0f;
        cooldownLeft = CooldownSeconds;
        Vector2 centre = CubeCanvasPoint(cam, out float radius);
        clicker.PowerClick(clicksWorth);
        PixelStats.Count("power.clicks");
        PixelAudio.Play("bomb_explode", 1.15f);
        PixelAudio.Play("overcharge", 1.4f);
        // Flash, shockwave, popup.
        flashT = 1f;
        waveT = 0f; waveCentre = centre;
        popupT = 0f; popupStart = centre + new Vector2(0f, radius * 1.1f);
        if (popup != null) popup.text = "POWER CLICK  x" + clicksWorth;
        clicker.SetCubeOffset(cam.transform.up * clicker.PixelBaseSize * 0.12f);   // a kick (it drifts back)
        shaking = true;
        fxTime = 0.12f;
    }

    private void Shake(float amount)
    {
        if (shakeAtFull <= 0f) return;
        float a = clicker.PixelBaseSize * shakeAtFull * amount;
        clicker.SetCubeOffset(new Vector3(Random.Range(-a, a), Random.Range(-a, a), Random.Range(-a, a)));
        shaking = true;
    }

    private void StopShake()
    {
        if (!shaking || clicker == null) return;
        shaking = false;
        clicker.SetCubeOffset(null);
    }

    // ------------------------------------------------------------------
    // UI: charge ring, cooldown ring, flash, shockwave, popup
    // ------------------------------------------------------------------

    private void EnsureUI()
    {
        if (canvasObject != null) return;
        canvasObject = PixelUIKit.CreateCanvas("Power Click", 590, new Vector2(1920f, 1080f), false);
        canvasObject.transform.SetParent(transform, false);
        canvasRect = canvasObject.GetComponent<RectTransform>();

        GameObject f = new GameObject("Flash", typeof(RectTransform), typeof(Image));
        f.transform.SetParent(canvasObject.transform, false);
        flash = f.GetComponent<Image>();
        flash.color = new Color(1f, 1f, 1f, 0f);
        flash.raycastTarget = false;
        PixelUIKit.Stretch(f.GetComponent<RectTransform>());

        ring = MakeArc("Charge Ring", chargeColor, 0.06f, out ringRect);
        cooldownRing = MakeArc("Cooldown Ring", cooldownColor, 0.025f, out cooldownRect);
        wave = MakeArc("Shockwave", readyColor, 0.04f, out waveRect);

        popup = PixelUIKit.CreateText(clicker != null ? clicker.UIFont : null, canvasObject.transform, "Popup", "", 64f, TextAlignmentOptions.Center, FontStyles.Bold, readyColor);
        popup.raycastTarget = false;
        popupRect = popup.rectTransform;
        popupRect.anchorMin = popupRect.anchorMax = new Vector2(0.5f, 0.5f);
        popupRect.pivot = new Vector2(0.5f, 0.5f);
        popupRect.sizeDelta = new Vector2(900f, 90f);
        popup.gameObject.SetActive(false);
    }

    private PixelArcGraphic MakeArc(string name, Color c, float halfWidth, out RectTransform rect)
    {
        GameObject g = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        g.transform.SetParent(canvasObject.transform, false);
        PixelArcGraphic arc = g.AddComponent<PixelArcGraphic>();
        arc.Setup(0.9f, halfWidth, 1.5f);
        arc.StartDegrees = 0f;
        arc.EndDegrees = 359.5f;
        arc.color = c;
        arc.raycastTarget = false;
        rect = arc.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        g.SetActive(false);
        return arc;
    }

    /// <summary>The cube's centre in the canvas (anchored at the middle) and its radius on screen, in canvas units.</summary>
    private Vector2 CubeCanvasPoint(Camera cam, out float radius)
    {
        Vector3 world = clicker.LiveWorldPosition;
        Vector3 s0 = cam.WorldToScreenPoint(world);
        Vector3 s1 = cam.WorldToScreenPoint(world + cam.transform.up * clicker.PixelBaseSize * 0.5f);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, s0, null, out Vector2 c0);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, s1, null, out Vector2 c1);
        radius = Mathf.Max(20f, (c1 - c0).magnitude);
        return c0;
    }

    private void LayoutRings(Camera cam, Vector2 pointer)
    {
        if (canvasObject == null) return;
        bool show = state == State.Charging || state == State.Ready;
        bool showCooldown = !show && cooldownLeft > 0f && !PixelCalm.InMenu && clicker.IsLiveCubeAt(pointer);
        ring.gameObject.SetActive(show);
        cooldownRing.gameObject.SetActive(showCooldown);
        if (!show && !showCooldown) return;

        Vector2 centre = CubeCanvasPoint(cam, out float radius);
        if (show)
        {
            float pulse = state == State.Ready ? 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 18f) : 1f;
            ringRect.anchoredPosition = centre;
            ringRect.sizeDelta = Vector2.one * radius * 3.2f * pulse;
            ring.StartDegrees = 0f;
            ring.EndDegrees = Mathf.Max(1f, 359.5f * charge);
            ring.color = state == State.Ready ? readyColor : Color.Lerp(chargeColor, readyColor, charge * 0.5f);
        }
        if (showCooldown)
        {
            cooldownRect.anchoredPosition = centre;
            cooldownRect.sizeDelta = Vector2.one * radius * 3f;
            float left = CooldownSeconds > 0f ? Mathf.Clamp01(cooldownLeft / CooldownSeconds) : 0f;
            cooldownRing.StartDegrees = 0f;
            cooldownRing.EndDegrees = Mathf.Max(1f, 359.5f * left);
        }
    }

    private void UpdateFx()
    {
        if (canvasObject == null) return;
        float dt = Time.unscaledDeltaTime;
        if (fxTime > 0f) { fxTime -= dt; if (fxTime <= 0f) StopShake(); }   // the kick ends, the cube drifts back

        flashT = Mathf.Max(0f, flashT - dt / 0.35f);
        flash.color = new Color(1f, 1f, 1f, flashStrength * flashT * flashT);

        if (waveT < 1f)
        {
            waveT = Mathf.Min(1f, waveT + dt / 0.6f);
            wave.gameObject.SetActive(true);
            float e = 1f - (1f - waveT) * (1f - waveT);
            waveRect.anchoredPosition = waveCentre;
            waveRect.sizeDelta = Vector2.one * Mathf.Lerp(120f, 1400f, e);
            wave.color = new Color(readyColor.r, readyColor.g, readyColor.b, 1f - waveT);
        }
        else if (wave.gameObject.activeSelf) wave.gameObject.SetActive(false);

        if (popupT < 1f)
        {
            popupT = Mathf.Min(1f, popupT + dt / 1.4f);
            popup.gameObject.SetActive(true);
            float grow = popupT < 0.12f ? Mathf.Lerp(0.4f, 1.15f, popupT / 0.12f) : Mathf.Lerp(1.15f, 1f, Mathf.Clamp01((popupT - 0.12f) / 0.15f));
            popupRect.localScale = Vector3.one * grow;
            popupRect.anchoredPosition = popupStart + new Vector2(0f, 120f * popupT);
            popup.color = new Color(readyColor.r, readyColor.g, readyColor.b, popupT < 0.7f ? 1f : 1f - (popupT - 0.7f) / 0.3f);
        }
        else if (popup.gameObject.activeSelf) popup.gameObject.SetActive(false);
    }
}
