// ---- System notes ----
// PixelClickerHazards.cs
//   Part of partial class PixelClicker: hazard blocks (Pest = Farming, Gas Pocket = Mining, Termite Hive = Woodcutting, and the other skills'
//   hazards) - their settings (Pest bug swarm, Termite Hive), charging up and leaving by themselves (UpdateHazardCharges, UpdateHazardExpiry),
//   defusing with right clicks (DefuseHazard, FinishDefuse, DefusePopup), clearing (ClearHazardNow), going off (TriggerHazard) and their
//   per-skill stats (CountHazardForSkill). Which block is a hazard is a block tag (IsHazard, PixelBlockTags); penalties are in PixelSkills.
//   Split out of PixelClicker.cs (no behaviour change).
// ---------------------------------------------------
using Unity.Profiling;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

public partial class PixelClicker
{
    // ------------------------------------------------------------------
    // Hazard blocks (Pest, Gas Pocket)
    // ------------------------------------------------------------------

    /// <summary>When true the auto clicker clicks hazard blocks too and clears them safely (a future upgrade). Off: it leaves them alone.</summary>
    public static bool HazardAutoClear = false;

    [Min(0f)]
    [Tooltip("How long a hazard block (Pest, Gas Pocket) stays on the cube before it goes away by itself, in seconds. 0 = the coded default (7).")]
    [SerializeField] private float hazardSeconds = 0f;

    [Tooltip("Turns off the Pest's bug swarm (a Pest that goes off just kills every sprout at once, as before).")]
    [SerializeField] private bool disablePestSwarm = false;

    [Min(0)]
    [Tooltip("Bugs the Pest sends at every sprout in your field. 0 = the coded default (6).")]
    [SerializeField] private int pestBugsPerSprout = 0;

    [Min(0)]
    [Tooltip("Bugs a Pest releases even when there is nothing to eat. 0 = the coded default (8).")]
    [SerializeField] private int pestBugsMin = 0;

    [Min(0f)]
    [Tooltip("How fast the pest bugs run, in old-pixel widths per second. 0 = the coded default (3.5).")]
    [SerializeField] private float pestBugSpeed = 0f;

    [Min(0f)]
    [Tooltip("Seconds one bug needs to gnaw through a sprout (more bugs on it are faster). 0 = the coded default (4).")]
    [SerializeField] private float pestChewSeconds = 0f;

    public int PestBugsPerSprout => pestBugsPerSprout > 0 ? pestBugsPerSprout : 6;
    public int PestBugsMin => pestBugsMin > 0 ? pestBugsMin : 8;
    public float PestBugSpeed => pestBugSpeed > 0f ? pestBugSpeed : 3.5f;
    public float PestChewSeconds => pestChewSeconds > 0f ? pestChewSeconds : 4f;

    [Header("Termite Hive (Woodcutting's hazard)")]
    [Tooltip("Turns off the termites: a Termite Hive that goes off then only costs the usual hazard XP penalty.")]
    [SerializeField] private bool disableTermites = false;
    [Min(0f)]
    [Tooltip("How fast a termite runs, in cube widths per second. 0 = the coded default (3.8).")]
    [SerializeField] private float termiteSpeed = 0f;
    [Min(0f)]
    [Tooltip("Seconds a termite needs to eat an old pixel; the bar over the pixel counts this down. Click the pixel before it ends to save it. 0 = the coded default (5).")]
    [SerializeField] private float termiteEatSeconds = 0f;
    [Min(0)]
    [Tooltip("The most termites (= old pixels attacked) one hive releases. 0 = the coded default (60).")]
    [SerializeField] private int termiteMax = 0;

    [Min(0)]
    [Tooltip("Right clicks that clear a Termite Hive (left clicks needed to break it are the pack's Clicks To Collect, 4). 0 = the coded default (10).")]
    [SerializeField] private int termiteRightClicks = 0;

    [Min(0)]
    [Tooltip("What clearing a hazard with RIGHT clicks pays, in that hazard's own currency (it also drops as an old pixel). Breaking one with left clicks pays nothing. 0 = the coded default (1).")]
    [SerializeField] private int hazardClearReward = 0;
    public float TermiteSpeed => termiteSpeed > 0f ? termiteSpeed : 3.8f;
    public float TermiteEatSeconds => termiteEatSeconds > 0f ? termiteEatSeconds : 5f;
    public int TermiteMax => termiteMax > 0 ? termiteMax : 60;

    [Min(0f)]
    [Tooltip("Gas Pocket blast: old pixels within this many old-pixel widths of the cube are blown away (no payout). 0 = the coded default (4).")]
    [SerializeField] private float gasBlastWidths = 0f;

    /// <summary>A hazard block that sat on the cube too long leaves by itself (the cube rolls a new pixel).</summary>
    [Tooltip("Seconds a hazard is on the cube before a charge against it (e.g. a Pesticide charge against the Termite Hive) clears it automatically, so you see it happen.")]
    [SerializeField] private float chargeClearDelay = 0.35f;

    /// <summary>A charge against the hazard on the cube (PixelBlockRules: Pesticide charges vs the Termite Hive) clears it automatically, using one charge.</summary>
    private void UpdateHazardCharges()
    {
        if (pixelTransform == null || isSpawning || tiers == null || tiers.Length == 0 || CubeHidden) return;
        PixelTier t = GetClickTier();
        if (!IsHazard(t.type) || Time.time - liveSince < Mathf.Max(0f, chargeClearDelay)) return;
        if (!PixelBlockRules.UseCharge(t.type)) return;
        PixelStats.Count("charges.used." + t.type);
        ClearHazardNow(t);
    }

    private void UpdateHazardExpiry()
    {
        if (pixelTransform == null || isSpawning || tiers == null || tiers.Length == 0) return;
        PixelTier t = GetClickTier();
        if (!IsHazard(t.type) || Time.time - liveSince < (hazardSeconds > 0f ? hazardSeconds : 7f)) return;
        hitsOnCurrentPixel = 0;
        if (!disableClickPops) PixelBasicFx.Play(t.type, t.color, pixelTransform.position, PixelBaseSize, true, 1f, clickPopStrength);
        if (randomizeSpawnTier) currentTierIndex = RollTier();
        Materialize(GetClickTier());
    }

    /// <summary>Is the block on the cube a hazard right now? 'remaining' = 1 just appeared .. 0 about to leave by itself; 'since' = when it appeared (PixelHazardTell).</summary>
    public bool TryGetLiveHazard(out PixelType type, out float remaining, out float since)
    {
        type = PixelType.White; remaining = 0f; since = liveSince;
        if (pixelTransform == null || tiers == null || tiers.Length == 0 || CubeHidden) return false;
        PixelTier t = GetClickTier();
        if (!IsHazard(t.type)) return false;
        type = t.type;
        remaining = Mathf.Clamp01(1f - (Time.time - liveSince) / (hazardSeconds > 0f ? hazardSeconds : 7f));
        return true;
    }

    private int defuseClicks;

    /// <summary>When the last right click on a hazard / bottle happened (Block Hold ignores a right press that follows it closely: it is part of the same spam).</summary>
    public float LastDefuseTime { get; private set; } = -99f;

    /// <summary>
    /// A right click on a hazard block: it takes as many right clicks as the block takes to break PLUS the hits you had already put into it (5 to break
    /// and 2 hits in = 7 right clicks). Reaching that clears the block safely - no explosion / swarm, no XP penalty, no payout.
    /// </summary>
    private void DefuseHazard()
    {
        PixelTier tier = GetClickTier();
        if (!IsHazard(tier.type) || pixelTransform == null) return;
        int required = tier.type == PixelType.HornetNest ? (termiteRightClicks > 0 ? termiteRightClicks : 10) : NeededClicks(tier) + hitsOnCurrentPixel;   // (the Termite Hive: a flat 10 right clicks)
        LastDefuseTime = Time.unscaledTime;
        defuseClicks++;
        hitPunchTimer = hitPunchDuration;
        PixelAudio.Play("click");
        StartCoroutine(DefusePopup(Mathf.Min(defuseClicks, required) + "/" + required, pixelTransform.position + Vector3.up * PixelBaseSize * 0.7f));
        if (defuseClicks < required) return;
        FinishDefuse(tier, true);
    }

    /// <summary>A held Pesticide (PixelBlockRules): the hazard on the cube is cleared by this one click, safely (no explosion / swarm, no XP penalty, no payout).</summary>
    private void ClearHazardNow(PixelTier tier)
    {
        if (pixelTransform == null) return;
        LastDefuseTime = Time.unscaledTime;
        hitPunchTimer = hitPunchDuration;
        PixelAudio.Play("click");
        FinishDefuse(tier);
    }

    /// <summary>Per-skill hazard counters for the Stats screen ('hazards.triggered.woodcutting'...): the skill whose pack owns the hazard block.</summary>
    private static void CountHazardForSkill(string prefix, PixelTier tier)
    {
        PixelSkills s = PixelSkills.Instance;
        PixelSkills.SkillPack pack = s != null && tier != null ? s.PackOfType(tier.type) : null;
        if (pack != null) PixelStats.Count(prefix + pack.id, 1);
    }

    private void FinishDefuse(PixelTier tier, bool byRightClick = false)
    {
        if (byRightClick)
        {
            // Worked away with right clicks: it is collected as currency and drops as an old pixel (left clicking a hazard to pieces pays nothing).
            int idx = CurrentClickTierIndex;
            double reward = hazardClearReward > 0 ? hazardClearReward : 1;
            if (IsValidTier(idx)) { AddCurrency(idx, reward); if (spawnFallingCopy) SpawnFallingCopy(idx, reward); }
        }
        defuseClicks = 0;
        hitsOnCurrentPixel = 0;
        PixelStats.Count("hazards.defused", 1);
        CountHazardForSkill("hazards.defused.", tier);
        PixelHints.Announce(tier.type == PixelType.Mold ? "You scraped the mold off the cube. What it already spread stays - shake infected pixels clean." : "You cleared the " + tier.displayName.ToLowerInvariant() + " safely.");
        if (!disableClickPops) PixelBasicFx.Play(tier.type, tier.color, pixelTransform.position, PixelBaseSize, false, 1f, clickPopStrength);
        if (randomizeSpawnTier) currentTierIndex = RollTier();
        Materialize(GetClickTier());
    }

    /// <summary>A small number that rises off the cube and fades ('3/7').</summary>
    private IEnumerator DefusePopup(string text, Vector3 start)
    {
        GameObject go = new GameObject("Defuse Text");
        go.transform.position = start;
        TextMeshPro tm = go.AddComponent<TextMeshPro>();
        if (uiFont != null) tm.font = uiFont;
        tm.text = text;
        tm.fontSize = 5f * Mathf.Max(0.2f, PixelBaseSize);
        tm.fontStyle = FontStyles.Bold;
        tm.alignment = TextAlignmentOptions.Center;
        tm.color = new Color(1f, 0.75f, 0.3f, 1f);
#if UNITY_2023_1_OR_NEWER
        tm.textWrappingMode = TextWrappingModes.NoWrap;
#else
        tm.enableWordWrapping = false;
#endif
        for (float t = 0f; t < 0.7f; t += Time.unscaledDeltaTime)
        {
            if (go == null) yield break;
            if (targetCamera != null) go.transform.rotation = targetCamera.transform.rotation;
            go.transform.position = start + Vector3.up * (PixelBaseSize * 0.8f * (t / 0.7f));
            Color c = tm.color; c.a = 1f - t / 0.7f; tm.color = c;
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    /// <summary>The hazard block was hit too many times: the Pest kills every sprout, the Gas Pocket explodes. Either way the skill earns less XP for a while.</summary>
    private void TriggerHazard(int tierIndex, PixelTier tier)
    {
        hitsOnCurrentPixel = 0;
        PixelBlockRules.OnHazardWentOff(tier.type);   // some hazards (the Hornet Nest) cost the Block Hold meter part of its charge
        Vector3 pos = pixelTransform.position;
        string what;
        if (tier.type == PixelType.Pest)
        {
            int killed = disablePestSwarm ? SeedSprout.KillAll() : PixelPestSwarm.Release(this, pos);
            what = killed > 0 ? (disablePestSwarm ? "The pests ate " : "A swarm of pests is going for ") + killed + " sprout" + (killed == 1 ? "" : "s") + "!" : "The pests swarm out!";
            if (!disableFarmFx) PixelFarmFx.Break(PixelType.Weevil, pos, PixelBaseSize, tier.color);
        }
        else if (tier.type == PixelType.Toadstool)
        {
            what = "The toadstool burst in a cloud of dizzy spores - the cube keeps jerking away from your cursor!";
            if (!disableFarmFx) PixelFarmFx.Break(tier.type, pos, PixelBaseSize, tier.color);
            PixelToadstoolDaze.Begin(this);
        }
        else if (tier.type == PixelType.Wildfire)
        {
            what = "The wildfire spread and burned up the husks around the cube!";
            if (!disableFireFx) FireBurstFx.Play(pos, PixelBaseSize * 1.6f);
            float burnReach = OldPixelWorldSize * (gasBlastWidths > 0f ? gasBlastWidths : 4f) * 1.5f;
            for (int i = oldPixels.Count - 1; i >= 0; i--)
            {
                Rigidbody b = oldPixels[i];
                if (b != null && (b.position - pos).sqrMagnitude <= burnReach * burnReach) ReleaseOldPixel(b, false);
            }
        }
        else if (tier.type == PixelType.Jellyfish)
        {
            what = "The jellyfish stung everything around it!";
            if (!disableFarmFx) PixelFarmFx.Break(tier.type, pos, PixelBaseSize, tier.color);
            float sporeReach = OldPixelWorldSize * (gasBlastWidths > 0f ? gasBlastWidths : 4f);
            if (tier.type == PixelType.Jellyfish)
            {
                sporeReach *= jellyfishBlastFactor > 0f ? jellyfishBlastFactor : 1.5f;
                int pellets = PixelBait.Count;
                PixelBait.DestroyAll(true);   // the shock fries every bait pellet
                if (pellets > 0) what += " The bait pellets were fried!";
                if (!disableFarmFx) FractureFx.Play(pos, PixelBaseSize, 16, 3.4f, 0.55f, new Color(1f, 0.9f, 1f, 1f), new Color(0.95f, 0.4f, 1f, 1f));
                PixelAudio.Play("jelly_sting");
            }
            for (int i = oldPixels.Count - 1; i >= 0; i--)
            {
                Rigidbody b = oldPixels[i];
                if (b != null && (b.position - pos).sqrMagnitude <= sporeReach * sporeReach) ReleaseOldPixel(b, false);
            }
        }
        else if (tier.type == PixelType.Mold)
        {
            what = "The mold burst and spread far over the fallen pixels - shake them clean!";
            PixelMold.Spread(pos, 2f);
            if (!disableFarmFx) PixelFarmFx.Break(PixelType.Mold, pos, PixelBaseSize, tier.color);
        }
        else if (tier.type == PixelType.HornetNest)   // (the Termite Hive: the enum value kept its old name so saves stay valid)
        {
            int targeted = disableTermites ? 0 : PixelTermiteSwarm.Release(this, pos);
            what = targeted > 0 ? "Termites poured out of the hive and are going for " + targeted + " old pixel" + (targeted == 1 ? "" : "s") + " - click them to save them!" : "The termites poured out but found nothing to eat.";
            if (!disableFarmFx) PixelFarmFx.Break(PixelType.HornetNest, pos, PixelBaseSize, tier.color);
        }
        else
        {
            what = "The gas pocket exploded!";
            if (!disableFireFx) FireBurstFx.Play(pos, PixelBaseSize);
            float reach = OldPixelWorldSize * (gasBlastWidths > 0f ? gasBlastWidths : 4f);
            for (int i = oldPixels.Count - 1; i >= 0; i--)
            {
                Rigidbody b = oldPixels[i];
                if (b != null && (b.position - pos).sqrMagnitude <= reach * reach) ReleaseOldPixel(b, false);
            }
        }
        PixelStats.Count("hazards.triggered", 1);
        CountHazardForSkill("hazards.triggered.", tier);
        string penalty = PixelSkills.ApplyHazardPenalty();
        PixelHints.Announce(what, what + " " + penalty);
        PlayClickEffects(tier);
        if (randomizeSpawnTier) currentTierIndex = RollTier();
        Materialize(GetClickTier());
        onPixelClicked?.Invoke();
    }
}
