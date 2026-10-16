// ---- System notes ----
// PixelBlockHold.cs
//   Block Hold (shop pack with unlocksBlockHold, Upgrades tab, cheap while testing; component added by PixelShop / PixelClicker.Awake, Activate / Deactivate like the Bank):
//   HOLD the RIGHT mouse button on the block on the cube to pull it out of the rotation (PixelClicker.TryPullLive: the cube rolls a new block at once, so you can keep clicking
//   while it stays held). Letting go puts it back IN LINE (PixelClicker.ReturnHeldTier: it comes AFTER the block that is on the cube now, with the hits it had - RollTier hands it out - so a hazard that came up while you held is dealt with first and holding / releasing can never reroll the cube). The hold drains a meter (holdSeconds x GripFactor: half of it at the start - startHoldShare 0.5 - plus 10% of that half at Standard level 3 and again at level 5, gripPerkBoost); letting go starts a short cooldown (releaseCooldownSeconds) before the meter recharges
//   (rechargeSeconds for empty -> full). If the meter runs dry while holding, the block is put back anyway, the meter flashes red and the cooldown is longer
//   (exhaustCooldownSeconds). Can't be held: hazards, Bottle / Whale / Fish / Treasure, or while a fish is being reeled in. A block with hits CAN be held and keeps them.
//   Releasing a held block over a hazard moves the hazard to next in line (it is not removed). Filler can be held. The look (beam, aura, side slide, cursor arc meter) is described at the end of these notes. METER LOOK: the arc fill glides to the real value, goes blue -> amber -> red as it runs low (flickering faster the lower it gets), has a soft glow behind it, a bright pulsing tip riding the end of the fill, quarter-mark dots and a white "ready again" pop when a recharge finishes. All arc layers (track, glow, fill, ready pop) share ONE centre line (RingMid) and have round caps (PixelArcGraphic: a mesh band with round caps, no radial fill); the tip sits on that line and is about the bar's thickness. The meter only shows while the mouse is on the cube or a block is held (meterOnlyOnHover, 0.35 s grace while the next block materializes) and fades out over meterFadeSeconds (0.35) instead of vanishing. The hold lasts 5 seconds by default, 10 for a hold block (PixelBlockRules: Whetstone, Pesticide...).
//   HOLD BLOCKS: what a held block gives is data (PixelBlockRules.HoldRule: rapid extra clicks / payout / XP multiplier + a label shown beside the meter); Whetstone (Woodcutting, 3 hits) = 2 extra rapid clicks (x3 at once).
//   A partly broken block keeps its hits through the hold (TryPullLive -> ReturnHeldTier -> pendingHits), so a whetstone you only noticed after a click or two can still be grabbed.
//   A right press within rightSpamGapSeconds (0.45) of another right press or of a hazard right click never starts a hold (spamming a hazard away must not grab the next block).
//   STALL: PixelTier.stallsHold / stallSeconds (Great Oak): every click on such a block freezes the drain for stallSeconds and adds stallRefill (0.04) of the meter (PixelBlockHold.Stall), the meter turns icy while frozen.
//   UNLOCKED BY THE STANDARD SKILL TREE (node 'hold', requireTreeNode on; 2026-10-16 - before that it was available from the start; requireShopPack off; the shop pack is no longer needed).
//   Tree branches: 'lucky' (+10% stake win chance) -> 'highroller' (stakes grow 50% faster). STAKE (disableStake, stake* fields): holding one of the SIX BASIC blocks (IsStakeBlock: Black, Gray, White, Red, Green, Blue - nothing else)  for at least stakeMinSeconds
//   (0.75) puts a stake on it: the value grows 1 + stakeGrowthPerSecond (0.6) x seconds held; on release the outcome is rolled (stakeWinChance 0.6, +/- stakeWinChanceChangeAtFullHold over a full meter) -
//   pays that multiplier or stakeLossValue (0 = nothing) - and travels with the block through the queue (PixelClicker.nextInLineStake -> liveStake, payout x liveStake). It is revealed when the block comes
//   round again: its aura glows green / red (PixelClickerAura.StakeHighlight), a top status line says what it is worth and a good / bad sound plays. While holding, the label by the meter shows the value and the chance.
//   CLICKS: every click during a stake hold adds stakePerClick (0.1) to the stake (stakeClickBonus, in StakeValue); every energyEveryClicks (5) clicks give the meter
//   energyPerClicks (1%), holding or not (OnClick from PixelClicker.CollectInternal; autoClicksCount off = only your own clicks).
//   A first-time tip (stake_intro) shows after 25 manual clicks. Skill XP is not changed by the stake.
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
    [Tooltip("Seconds after you let go before the meter starts to recharge. 0 = the default (0.5).")]
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

    [Header("Availability")]
    [Tooltip("Off (default) = Block Hold works from the very start of the game. On = it needs the shop pack (unlocksBlockHold) first, like it used to.")]
    [SerializeField] private bool requireShopPack = false;
    [Tooltip("Block Hold is unlocked by the 'Block Hold' node of the Standard skill tree (the first skill point at Standard level 2). Off = available from the start.")]
    [SerializeField] private bool requireTreeNode = true;

    [Header("Stake (hold a basic block - black, gray, white, red, green, blue - to gamble on it)")]
    [Tooltip("Turn the stake off (holding then only pulls the block out of the rotation).")]
    [SerializeField] private bool disableStake = false;

    [Min(0f)]
    [Tooltip("How much the block's value grows per second of holding if the stake pays off (0 = the default 0.6: a 5 s hold pays x4).")]
    [SerializeField] private float stakeGrowthPerSecond = 0f;

    [Range(0f, 1f)]
    [Tooltip("The chance the stake pays off (0 = the default 0.6). Upgrades can raise it.")]
    [SerializeField] private float stakeWinChance = 0f;

    [Range(-0.5f, 0.5f)]
    [Tooltip("How the win chance changes by a full-meter hold (0 = the same chance however long you hold; negative = holding longer is riskier).")]
    [SerializeField] private float stakeWinChanceChangeAtFullHold = 0f;

    [Min(0f)]
    [Tooltip("A hold shorter than this many seconds is just a pull (no stake; quick taps still shuffle a filler). 0 = the default (0.75).")]
    [SerializeField] private float stakeMinSeconds = 0f;

    [Range(0f, 1f)]
    [Tooltip("What a LOST stake pays, as a fraction of the normal value (0 = the block is worth nothing).")]
    [SerializeField] private float stakeLossValue = 0f;

    [Header("Clicks")]
    [Min(0f)]
    [Tooltip("Each click while you hold a stake block adds this to its stake multiplier (0.1 = x1.5 becomes x1.6). 0 = off.")]
    [SerializeField] private float stakePerClick = 0.1f;
    [Min(0)]
    [Tooltip("Every this many clicks gives the hold meter 'Energy Per Clicks' (0 = off).")]
    [SerializeField] private int energyEveryClicks = 5;
    [Range(0f, 1f)]
    [Tooltip("Share of the hold meter given every 'Energy Every Clicks' clicks (0.01 = 1%).")]
    [SerializeField] private float energyPerClicks = 0.01f;
    [Tooltip("Do auto clicks count for the stake bonus and the meter energy? Off = only your own clicks.")]
    [SerializeField] private bool autoClicksCount = false;

    [Header("Look")]
    [Tooltip("Show the hold meter only while the mouse is over the cube (or a block is held). Off = whenever it is not full.")]
    [SerializeField] private bool meterOnlyOnHover = true;
    [Min(0f)]
    [Tooltip("Seconds the hold meter takes to fade out after you leave the cube (or it fills up).")]
    [SerializeField] private float meterFadeSeconds = 0.35f;
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
    private static void ResetStatics() { Instance = null; dotSprite = null; }

    private float HoldSeconds => (holdSeconds > 0f ? holdSeconds : 5f) * GripFactor;

    [Header("Grip (Standard perks)")]
    [Range(0.05f, 1f)]
    [Tooltip("Share of the full hold length you get at the start (0.5 = half). Standard's grip perks add to it.")]
    [SerializeField] private float startHoldShare = 0.5f;
    [Min(0f)]
    [Tooltip("Each grip perk (Standard level 3 and level 5) adds this share of the STARTING hold length (0.1 = +10% of the halved hold).")]
    [SerializeField] private float gripPerkBoost = 0.1f;

    /// <summary>How much of the hold length the player has now: startHoldShare, + gripPerkBoost of it at Standard level 3 and again at level 5.</summary>
    private float GripFactor
    {
        get
        {
            int lvl = PixelSkills.LevelOfId(PixelSkills.StandardId);
            int perks = (lvl >= 3 ? 1 : 0) + (lvl >= 5 ? 1 : 0);
            return Mathf.Clamp(startHoldShare, 0.05f, 1f) * (1f + gripPerkBoost * perks);
        }
    }
    private float RechargeSeconds => rechargeSeconds > 0f ? rechargeSeconds : 8f;
    private float ReleaseCooldown => releaseCooldownSeconds > 0f ? releaseCooldownSeconds : 0.5f;
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
    private PixelArcGraphic fill, back;
    private TMPro.TMP_Text bonusLabel;
    private PixelArcGraphic glow, ready;
    private Image tip, pill;
    private TMPro.TMP_Text readout, caption;
    private RectTransform readoutRect;
    private readonly Image[] ticks = new Image[3];
    private bool wasFull = true;
    private static Sprite dotSprite;
    private const float ArcHalfDegrees = 55f;

    public bool Active => (unlocked || !requireShopPack) && !disableBlockHold && (!requireTreeNode || PixelSkills.HasStandardNode("hold"));
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

    private int energyClicks;     // clicks counted towards the next bit of meter energy
    private float stakeClickBonus; // what the clicks during this hold added to the stake

    /// <summary>A click on the cube (PixelClicker.CollectInternal): while a stake block is held it raises the stake by stakePerClick, and every energyEveryClicks clicks top the meter up by energyPerClicks.</summary>
    public static void OnClick(bool automatic)
    {
        PixelBlockHold h = Instance;
        if (h == null || !h.Active || (automatic && !h.autoClicksCount)) return;
        if (h.holding && h.stakePerClick > 0f && h.StakeEligible()) h.stakeClickBonus += h.stakePerClick;
        if (h.energyEveryClicks > 0 && ++h.energyClicks >= h.energyEveryClicks)
        {
            h.energyClicks = 0;
            h.meter = Mathf.Min(1f, h.meter + h.energyPerClicks);
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

    private void Awake() { Instance = this; clicker = GetComponent<PixelClicker>(); if (clicker == null) clicker = PixelFind.First<PixelClicker>(); }

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
        if (PixelCalm.InMenu)
        {
            if (holding) Release(false);
            FinishReturn();   // (no slide while paused: the block goes straight back)
            return;
        }

        float dt = Time.unscaledDeltaTime;
        if (holding)
        {
            if (!PixelInput.RightHeld()) Release(false);
            else if (UpdateCharging(dt)) { }   // a charge block (Pesticide) was used up
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
                else deferredSince = Time.unscaledTime;   // too soon / the new block is still materializing: if the button is STILL held a moment later, the hold starts then (no need to let go and move the mouse)
            }
            else if (deferredSince > 0f)
            {
                if (!PixelInput.RightHeld() || Time.unscaledTime - deferredSince > DeferWindow) deferredSince = -1f;
                else if (Time.unscaledTime - lastRightPress >= SpamGap && Time.unscaledTime - clicker.LastDefuseTime >= SpamGap && CanStart()) { deferredSince = -1f; Begin(); }
            }
        }
        if (flash > 0f) flash -= dt;
    }

    private float deferredSince = -1f;
    private const float DeferWindow = 1.5f;

    private bool CanStart()
    {
        if (meter < MinStart || PixelInput.PointerOverUI() || PixelClicker.ExternalClickBlock) return false;
        return clicker.IsLiveCubeAt(PixelInput.PointerPosition());   // (old pixels in front of the cube, e.g. the one a cleared hazard drops, do not stop the grab)
    }

    private float holdStartedAt;

    private float StakeGrowth => (stakeGrowthPerSecond > 0f ? stakeGrowthPerSecond : 0.6f) * (PixelSkills.HasStandardNode("highroller") ? 1.5f : 1f);   // Standard tree 'High roller': +50%
    private float StakeMin => stakeMinSeconds > 0f ? stakeMinSeconds : 0.75f;
    private float StakeBaseWin => stakeWinChance > 0f ? stakeWinChance : 0.6f;

    /// <summary>Only the six basic pixels (the monochrome ones - Black, Gray, White - and the RGB ones) can carry a stake.</summary>
    public static bool IsStakeBlock(PixelClicker.PixelType type)
    {
        return type == PixelClicker.PixelType.Black || type == PixelClicker.PixelType.Gray || type == PixelClicker.PixelType.White
            || type == PixelClicker.PixelType.Red || type == PixelClicker.PixelType.Green || type == PixelClicker.PixelType.Blue;
    }

    /// <summary>Can the block in hand carry a stake? (see IsStakeBlock)</summary>
    private bool StakeEligible()
    {
        if (disableStake || clicker == null || !clicker.IsValidTierIndex(heldTier)) return false;   // (heldTier is still set while Release runs: 'holding' already is false then)
        return IsStakeBlock(clicker.Tiers[heldTier].type);
    }

    /// <summary>The paragraph the tooltips, the Pixel Guide and the database add to a basic pixel's description, with the live numbers ("" when the stake is off).</summary>
    public static string StakeNote()
    {
        PixelBlockHold h = Instance;
        if (h != null && h.disableStake) return "";
        float growth = h != null ? h.StakeGrowth : 0.6f, win = h != null ? h.StakeBaseWin : 0.6f, minSeconds = h != null ? h.StakeMin : 0.75f;
        float full = h != null ? h.HoldSeconds : 5f, drift = h != null ? h.stakeWinChanceChangeAtFullHold : 0f, loss = h != null ? h.stakeLossValue : 0f;
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append("\n\n<b>Stake block:</b> hold it with Block Hold (the right button on the cube) for at least ").Append(minSeconds.ToString("0.##")).Append(" s to put a stake on it. ");
        sb.Append("The longer you hold, the more it is worth when it comes round again (+").Append(Mathf.RoundToInt(growth * 100f)).Append("% of its value per second, x").Append((1f + growth * full).ToString("0.#")).Append(" after a full hold), ");
        sb.Append("but the stake only pays off ").Append(Mathf.RoundToInt(win * 100f)).Append("% of the time");
        if (!Mathf.Approximately(drift, 0f)) sb.Append(" (").Append(Mathf.RoundToInt(Mathf.Clamp(win + drift, 0.05f, 0.95f) * 100f)).Append("% after a full hold)");
        sb.Append(loss > 0f ? " - a lost stake pays only x" + loss.ToString("0.##") + "." : " - a lost stake makes it worth nothing.");
        return sb.ToString();
    }

    private float StakeProgress(float heldSeconds) => Mathf.Clamp01(heldSeconds / HoldSeconds);
    private float StakeValue(float heldSeconds) => 1f + StakeGrowth * heldSeconds + stakeClickBonus;   // (+ stakePerClick for every click during the hold)
    private float StakeWin(float heldSeconds) => Mathf.Clamp(StakeBaseWin + (PixelSkills.HasStandardNode("lucky") ? 0.1f : 0f) + stakeWinChanceChangeAtFullHold * StakeProgress(heldSeconds), 0.05f, 0.95f);   // Standard tree 'Lucky stake': +10%

    // ---- Charge blocks (PixelBlockRules.HoldRule.chargesWhenHeld: Pesticide) ----

    private float chargeTimer;
    private int chargesGiven;

    /// <summary>While a charge block is held it shrinks and hands out its charges; returns true when it has been used up (the hold is over).</summary>
    private bool UpdateCharging(float dt)
    {
        if (!TryGetHeldType(out PixelClicker.PixelType type) || !PixelBlockRules.TryGetHold(type, out PixelBlockRules.HoldRule rule) || rule.chargesWhenHeld <= 0) return false;
        float seconds = Mathf.Max(0.1f, rule.chargeSeconds);
        chargeTimer += dt;
        int due = Mathf.Min(rule.chargesWhenHeld, Mathf.FloorToInt(chargeTimer / seconds * rule.chargesWhenHeld + 0.0001f));
        while (chargesGiven < due)
        {
            chargesGiven++;
            PixelBlockRules.AddCharges(rule.chargeHazard, 1);
            PixelAudio.Play("click", 1f + 0.15f * chargesGiven);
        }
        if (holder != null)   // it shrinks down to husk size (an old pixel's size), then pops off as a husk
        {
            float huskScale = clicker.PixelBaseSize > 0f ? Mathf.Clamp01(clicker.OldPixelWorldSize / clicker.PixelBaseSize) : 0.5f;
            holder.transform.localScale = Vector3.one * Mathf.Lerp(1f, huskScale, Mathf.Clamp01(chargeTimer / seconds));
        }
        if (chargeTimer < seconds) return false;
        UseUp(type, rule);
        return true;
    }

    /// <summary>The charge block in hand is used up: it pops off as a husk (an old pixel worth nothing; no currency) and does not go back in line.</summary>
    private void UseUp(PixelClicker.PixelType type, PixelBlockRules.HoldRule rule)
    {
        int usedTier = heldTier;
        Vector3 at = holder != null ? holder.transform.position : clicker.LiveWorldPosition;
        holding = false;
        heldTier = -1;
        returning = false;
        DestroyVisual();
        if (clicker.IsValidTierIndex(usedTier))
        {
            Vector2 side = Random.insideUnitCircle.normalized * 1.2f;
            clicker.SpawnStoredPixel(usedTier, 0d, at, new Vector3(side.x, 3.5f, side.y));   // the husk pops off (pixel rules: material, it pays nothing)
        }
        cooldown = ReleaseCooldown;
        PixelStats.Count("blocks.used." + type);
        PixelAudio.Play("ui_click");
        string block = NameOf(type), hazard = NameOf(rule.chargeHazard);
        PixelHints.Announce("+" + chargesGiven + " " + block + " charge" + (chargesGiven == 1 ? "" : "s") + " (" + PixelBlockRules.Charges(rule.chargeHazard) + " in all): each clears a " + hazard + " automatically.");
    }

    private string NameOf(PixelClicker.PixelType type)
    {
        int i = clicker != null ? clicker.IndexOf(type) : -1;
        return i >= 0 ? clicker.Tiers[i].displayName : type.ToString();
    }

    private void Begin()
    {
        deferredSince = -1f;
        if (!clicker.TryPullLive(out heldTier, out heldHits)) return;
        chargeTimer = 0f;
        chargesGiven = 0;
        holding = true;
        PixelStats.Count("hold.count");   // (Stats: blocks held)
        stakeClickBonus = 0f;
        holdStartedAt = Time.unscaledTime;
        returning = false;
        slide = 0f;
        BuildVisual();
        PixelAudio.Play("ui_click");
    }

    private void Release(bool exhausted)
    {
        // A charge block that already gave charges is used up, even when let go early (no free charges from tapping it again and again).
        if (chargesGiven > 0 && TryGetHeldType(out PixelClicker.PixelType chargeType) && PixelBlockRules.TryGetHold(chargeType, out PixelBlockRules.HoldRule chargeRule) && chargeRule.chargesWhenHeld > 0)
        {
            UseUp(chargeType, chargeRule);
            return;
        }
        holding = false;
        if (heldTier >= 0 && !exhausted) PixelShuffleBonus.OnRelease(heldTier, Time.unscaledTime - holdStartedAt);   // a quick tap on a filler = a shuffle (PixelShuffleBonus)
        // The block goes first in line (it comes after the block on the cube now); the picture of it only slides back for show.
        float stake = 1f;
        float heldFor = Time.unscaledTime - holdStartedAt;
        if (heldTier >= 0 && heldFor >= StakeMin && StakeEligible())
        {
            stake = Random.value < StakeWin(heldFor) ? StakeValue(heldFor) : stakeLossValue;
            PixelStats.Count("stake.placed");   // (the session recorder and Stats: stakes made; won / lost are counted when they are revealed)
        }   // the outcome is decided now but only shown when the block comes round again
        if (heldTier >= 0 && clicker != null) clicker.ReturnHeldTier(heldTier, heldHits, stake);
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

    private void OnEnable() { if (clicker == null) clicker = GetComponent<PixelClicker>(); if (clicker == null) clicker = PixelFind.First<PixelClicker>(); if (clicker != null) clicker.StakeAppeared += OnStakeAppeared; }
    private void OnDisable() { if (clicker != null) clicker.StakeAppeared -= OnStakeAppeared; PixelClickerAura.StakeHighlight = new Color(0f, 0f, 0f, 0f); }

    private bool introTried;

    /// <summary>A staked block came round: green and a message when it pays, red when the stake was lost.</summary>
    private void OnStakeAppeared(float stake)
    {
        bool win = stake > 1f;
        // Stats: counted when the outcome is SHOWN (not when it is rolled), so the Stats screen never gives it away early.
        PixelStats.Count(win ? "stake.won" : "stake.lost");
        if (win) PixelStats.Best("stake.best", stake);
        string name = clicker != null && clicker.IsValidTierIndex(clicker.CurrentClickTierIndex) ? clicker.Tiers[clicker.CurrentClickTierIndex].type.ToString() : "block";
        PixelStatusLines.Set("stake.live", win ? "Stake paid off: this " + name + " is worth x" + stake.ToString("0.0") : (stake <= 0f ? "Stake lost: this " + name + " is worth nothing" : "Stake lost: this " + name + " is worth x" + stake.ToString("0.0")),
                             win ? new Color(0.45f, 1f, 0.55f, 1f) : new Color(1f, 0.4f, 0.35f, 1f), 3f);
        PixelAudio.Play(win ? "traffic_good" : "traffic_bad");
    }

    private void LateUpdate()
    {
        if (clicker == null) return;
        // The block on the cube carries a stake: its aura glows green (it pays) or red (lost).
        float live = clicker.LiveStake;
        PixelClickerAura.StakeHighlight = Active && !Mathf.Approximately(live, 1f) ? (live > 1f ? new Color(0.3f, 1f, 0.45f, 1f) : new Color(1f, 0.25f, 0.2f, 1f)) : new Color(0f, 0f, 0f, 0f);
        if (!introTried && Active && !disableStake && !PixelTitleScreen.Showing && !PixelHints.IntroHoldsGuide)
        {
            PixelStats stats = PixelFind.Cached<PixelStats>();
            if (stats != null && stats.ManualClicks >= 25) { introTried = true; PixelHints.Trigger("stake_intro"); }   // (shown once)
        }
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
    // The meter: a curved vertical arc to the RIGHT of the mouse cursor with a big percentage, a state caption and the bonus text beside it
    // ------------------------------------------------------------------

    // The arc's shape, shared by every layer so they all sit on ONE curve: centre line at RingMid of the half-size, rounded ends.
    private const float RingMid = 0.88f, RingHalf = 0.09f;

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

    private float lastCubeHover = -99f;
    private float meterAlpha;
    private CanvasGroup meterGroup;

    /// <summary>The meter only shows while the mouse is on the cube (or a block is held); a short grace covers the moment the next block materializes.</summary>
    private bool MeterHovered()
    {
        if (!meterOnlyOnHover || holding) return true;
        if (clicker != null && !PixelInput.PointerOverUI() && clicker.IsLiveCubeAt(PixelInput.PointerPosition())) lastCubeHover = Time.unscaledTime;
        return Time.unscaledTime - lastCubeHover < 0.35f;
    }

    private void UpdateMeter()
    {
        bool full = meter >= 0.999f;
        bool allowed = Active && !PixelTitleScreen.Showing;
        bool show = allowed && (holding || returning || flash > 0f || cooldown > 0f || !full || readyPop > 0f) && MeterHovered();
        // Fades in quickly and out over meterFadeSeconds (leaving the cube, or the meter filling up); gone at once on the title screen.
        float fadeDt = Time.unscaledDeltaTime;
        meterAlpha = !allowed ? 0f : Mathf.MoveTowards(meterAlpha, show ? 1f : 0f, fadeDt / (show ? 0.12f : Mathf.Max(0.01f, meterFadeSeconds)));
        if (meterAlpha <= 0f) { if (canvasRoot != null && canvasRoot.activeSelf) canvasRoot.SetActive(false); wasFull = full; return; }
        if (canvasRoot == null) BuildMeter();
        if (canvasRoot == null) return;
        canvasRoot.SetActive(true);
        if (meterGroup != null) meterGroup.alpha = meterAlpha;
        float dt = Time.unscaledDeltaTime;
        float time = Time.unscaledTime;

        // The "ready again" pop: the moment a recharge finishes.
        if (full && !wasFull && !holding) readyPop = 0.45f;
        wasFull = full;
        if (readyPop > 0f) readyPop -= dt;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, PixelInput.PointerPosition(), null, out Vector2 local))
            panel.anchoredPosition = local + new Vector2(-22f, 0f);   // (the arc sits on the right of the panel, ~30-90 px right of the cursor)

        PixelBlockRules.HoldRule bonus = holding ? PixelBlockRules.ActiveHold : null;
        string labelText = bonus != null && !string.IsNullOrEmpty(bonus.label) ? bonus.label : null;
        if (labelText == null && holding && StakeEligible() && Time.unscaledTime - holdStartedAt >= StakeMin)
        {
            float heldFor = Time.unscaledTime - holdStartedAt;
            labelText = "Stake x" + StakeValue(heldFor).ToString("0.0") + "  (" + Mathf.RoundToInt(StakeWin(heldFor) * 100f) + "% pays)";   // the value grows while you hold; the chance is shown too
        }
        if (bonusLabel != null)
        {
            bool on = labelText != null;
            if (bonusLabel.gameObject.activeSelf != on) bonusLabel.gameObject.SetActive(on);
            if (on)
            {
                if (bonusLabel.text != labelText) bonusLabel.text = labelText;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, PixelInput.PointerPosition(), null, out Vector2 lp2))
                    bonusLabel.rectTransform.anchoredPosition = lp2 + new Vector2(104f, -62f);
            }
        }

        // The big readout beside the arc: the percentage and what the meter is doing.
        if (readout != null)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, PixelInput.PointerPosition(), null, out Vector2 lp3))
                readoutRect.anchoredPosition = lp3 + new Vector2(100f, 0f);
            string state = holding ? (Stalled ? "FROZEN" : "HOLDING") : flash > 0f ? "EMPTY" : cooldown > 0f ? "WAIT" : !full ? "RECHARGING" : "READY";
            int pct = Mathf.RoundToInt(Mathf.Clamp01(shown) * 100f);
            string text = pct + "%";
            if (readout.text != text) readout.text = text;
            if (caption.text != state) caption.text = state;
        }

        // The drawn fill glides to the real value (smooth drain and recharge).
        shown = Mathf.MoveTowards(shown, Mathf.Clamp01(meter), dt * 3.5f + Mathf.Abs(shown - meter) * 8f * dt);
        float k = Mathf.Clamp01(shown);
        float sweep = Mathf.Lerp(90f - ArcHalfDegrees, 90f + ArcHalfDegrees, k);   // degrees from the bottom (the arc sprite shows 35..145 degrees)
        float endDeg = -90f + sweep;   // the fill runs from the bottom end of the arc up to the current level, round at both ends
        fill.EndDegrees = endDeg;
        if (glow != null) glow.EndDegrees = endDeg;

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
        if (k < 0.01f) { Color none = c; none.a = 0f; fill.color = none; }   // empty: no stub at the bottom end
        if (readout != null) { Color rc2 = Color.Lerp(c, Color.white, 0.55f); rc2.a = 1f; readout.color = rc2; Color cc = c; cc.a = 1f; caption.color = cc; if (pill != null) pill.color = flashing ? Color.Lerp(new Color(0.04f, 0.05f, 0.09f, 0.78f), new Color(0.45f, 0f, 0f, 0.85f), pulse) : new Color(0.04f, 0.05f, 0.09f, 0.78f); }
        back.color = flashing ? Color.Lerp(new Color(0f, 0f, 0f, 0.6f), new Color(0.55f, 0f, 0f, 0.85f), pulse) : new Color(0f, 0f, 0f, 0.6f);

        // The soft glow behind the fill, pulsing while held.
        if (glow != null)
        {
            Color gc = c; gc.a = holding ? 0.28f + 0.14f * Mathf.Sin(time * 6f) : 0.12f;
            if (k < 0.01f) gc.a = 0f;
            glow.color = gc;
        }

        // The bright tip at the end of the fill: it rides the arc as the meter moves.
        if (tip != null)
        {
            float angle = (-90f + sweep) * Mathf.Deg2Rad;
            float radius = panel.sizeDelta.x * 0.5f * RingMid;   // exactly on the bar's centre line
            tip.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            float tipScale = (holding ? 1f + 0.12f * Mathf.Sin(time * 9f) : 0.85f) * (1f + (readyPop > 0f ? readyPop * 0.8f : 0f));
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
            ready.rectTransform.sizeDelta = panel.sizeDelta * (1f + (1f - rp) * 0.12f);
        }
    }

    private void BuildMeter()
    {
        canvasRoot = PixelUIKit.CreateCanvas("Block Hold", 55, new Vector2(1920f, 1080f), false);
        meterGroup = canvasRoot.AddComponent<CanvasGroup>();
        meterGroup.blocksRaycasts = false;
        meterGroup.interactable = false;
        canvasRect = canvasRoot.GetComponent<RectTransform>();

        GameObject p = new GameObject("Meter", typeof(RectTransform));
        p.transform.SetParent(canvasRoot.transform, false);
        panel = p.GetComponent<RectTransform>();
        panel.sizeDelta = new Vector2(230f, 230f);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);

        PixelArcGraphic Arc(string name, float halfWidth, float feather)
        {
            GameObject g = new GameObject(name, typeof(RectTransform), typeof(PixelArcGraphic));
            g.transform.SetParent(panel, false);
            PixelArcGraphic arc = g.GetComponent<PixelArcGraphic>();
            arc.raycastTarget = false;
            arc.Setup(RingMid, halfWidth, feather);
            arc.StartDegrees = -ArcHalfDegrees;
            arc.EndDegrees = ArcHalfDegrees;
            RectTransform r = g.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = panel.sizeDelta;
            r.anchoredPosition = Vector2.zero;
            return arc;
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
        // Every layer is the same curve (centre line RingMid) with round caps; the fill and glow grow from the bottom end to the current level.
        back = Arc("Back", RingHalf + 0.035f, 1.5f);
        glow = Arc("Glow", RingHalf + 0.06f, 9f);
        fill = Arc("Fill", RingHalf, 1.5f);
        ready = Arc("Ready", RingHalf + 0.035f, 2f);
        ready.color = new Color(1f, 1f, 1f, 0f);
        // Quarter marks: small dots on the track at 25 / 50 / 75 %.
        if (dotSprite == null) dotSprite = BuildDotSprite();
        float trackR = panel.sizeDelta.x * 0.5f * RingMid;
        for (int i = 0; i < ticks.Length; i++)
        {
            float a = (-90f + Mathf.Lerp(90f - ArcHalfDegrees, 90f + ArcHalfDegrees, (i + 1) * 0.25f)) * Mathf.Deg2Rad;
            ticks[i] = Dot("Tick " + (i + 1), new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * trackR, 9f);
        }
        tip = Dot("Tip", Vector2.zero, panel.sizeDelta.x * RingHalf * 1.5f);   // about the bar's thickness, so it sits ON the bar

        // The readout: a dark pill with the percentage and a state caption, just right of the arc.
        GameObject pg = new GameObject("Readout", typeof(RectTransform), typeof(Image));
        pg.transform.SetParent(canvasRoot.transform, false);
        pill = pg.GetComponent<Image>();
        pill.raycastTarget = false;
        PixelUIKit.StyleBox(pill);
        readoutRect = pg.GetComponent<RectTransform>();
        readoutRect.anchorMin = readoutRect.anchorMax = new Vector2(0.5f, 0.5f);
        readoutRect.pivot = new Vector2(0f, 0.5f);
        readoutRect.sizeDelta = new Vector2(132f, 74f);
        readout = PixelUIKit.CreateText(clicker.UIFont, pg.transform, "Percent", "100%", 40f, TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold, Color.white);
        readout.raycastTarget = false;
        RectTransform rr = readout.rectTransform;
        rr.anchorMin = new Vector2(0f, 0.38f); rr.anchorMax = Vector2.one; rr.offsetMin = rr.offsetMax = Vector2.zero;
        caption = PixelUIKit.CreateText(clicker.UIFont, pg.transform, "Caption", "READY", 17f, TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold, Color.white);
        caption.raycastTarget = false;
        RectTransform cr = caption.rectTransform;
        cr.anchorMin = Vector2.zero; cr.anchorMax = new Vector2(1f, 0.4f); cr.offsetMin = cr.offsetMax = Vector2.zero;

        // The bonus text of a held bonus block (PixelBlockRules.HoldRule.label), under the arc.
        bonusLabel = PixelUIKit.CreateText(clicker.UIFont, canvasRoot.transform, "Bonus Label", "", 30f, TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold, new Color(1f, 0.9f, 0.4f, 1f));
        bonusLabel.raycastTarget = false;
        RectTransform blr = bonusLabel.rectTransform;
        blr.anchorMin = blr.anchorMax = blr.pivot = new Vector2(0.5f, 0.5f);
        blr.sizeDelta = new Vector2(320f, 44f);
        blr.pivot = new Vector2(0f, 0.5f);
        bonusLabel.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
        bonusLabel.gameObject.SetActive(false);
    }
}
