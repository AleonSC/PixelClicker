// ---- System notes ----
// PixelClickerClick.cs
//   Part of PixelClicker (partial class): the click (Collect, PowerClick, AutoCollect, the fishing hand-off, CollectInternal = the click flow, and its steps FishClickStops / ToughHit / ClickPayout / PlayBreakEffects). The system notes are in PixelClicker.cs.
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
    // Core loop
    // ------------------------------------------------------------------

    /// <summary>Performs one click. Public so buttons or automation can call it too.</summary>
    public void Collect() => CollectInternal(false);

    /// <summary>
    /// Power click (PixelPowerClick, TEST): ONE manual click that counts as 'clicks' clicks - payout x clicks, tough blocks take that many hits,
    /// the combo grows by that many (the same path the dev 'clicks per click' setting uses, DevClickCount).
    /// </summary>
    public void PowerClick(int clicks)
    {
        int was = DevClickCount;
        DevClickCount = Mathf.Max(1, was) * Mathf.Max(1, clicks);
        try { CollectInternal(false); }
        finally { DevClickCount = was; }
    }

    /// <summary>
    /// A manual click from first person mode: the player aims at the cube from any distance. Follows the same rules as a mouse click
    /// (paused, blocked, intro lock, the minimum time between clicks). Returns true if it clicked.
    /// </summary>
    public bool ManualClickFromAfar()
    {
        if ((Time.timeScale <= 0f && !PixelTimeStop.IsStopped) || PixelGameMode.PauseMenuOpen) return false;
        if (clicksBlocked || GodMode || PixelCameraIntro.ClicksLocked) return false;
        if (Time.unscaledTime - lastManualClickTime < minClickInterval) return false;
        lastManualClickTime = Time.unscaledTime;
        Collect();
        return true;
    }

    /// <summary>A click made by automation (the auto clicker). Same as <see cref="Collect"/>, but flagged as automatic.</summary>
    public void AutoCollect() => CollectInternal(true);

    /// <summary>The Weak Spots round for a fish ended: caught = pay it like a normal click, otherwise it got away (no payout, the next pixel comes).</summary>
    private void FishingDone(bool caught)
    {
        fishingBusy = false;
        if (caught)
        {
            fishingPass = true;
            try { CollectInternal(false); }
            finally { fishingPass = false; }
            return;
        }
        PixelHints.Announce("The fish got away!");
        hitsOnCurrentPixel = 0;
        if (randomizeSpawnTier) currentTierIndex = RollTier();
        Materialize(GetClickTier());
    }

    private void CollectInternal(bool automatic)
    {
        if (!disableClickPulse) clickPulseTimer = clickPulseSeconds > 0f ? clickPulseSeconds : 0.12f;
        if (blockClicksWhileSpawning && isSpawning) return;
        int clickCount = Mathf.Max(1, DevClickCount);
        if (!automatic) PixelBlockRules.OnManualClick(this);   // a held Whetstone follows every click with a rapid burst of extra clicks

        int tierIndex = GetClickTierIndex();
        PixelTier tier = tiers[tierIndex];
        if (tier.type == PixelType.GoldenSnitch) { PixelGoldenSnitch.CubeClicked(automatic); return; }   // the Golden Snitch is never clicked off the cube: your click makes it fly (PixelGoldenSnitch)
        PixelBlockHold.OnClick(automatic);   // a stake being held grows +0.1 per click; every 5 clicks give the hold meter 1%
        if (tier.stallsHold) PixelBlockHold.Stall(tier.stallSeconds > 0f ? tier.stallSeconds : 1.5f, tier.stallRefill > 0f ? tier.stallRefill : 0.04f);   // every click on a stalling block freezes the hold meter and refills it a little
        if (!automatic && IsHazard(tier.type) && PixelBlockRules.HeldClearsHazard(tier.type)) { ClearHazardNow(tier); return; }   // a held Pesticide: one left click clears it
        if (automatic && IsHazard(tier.type) && !HazardAutoClear) return;   // the auto clicker never touches a hazard: it waits until you deal with it (or it goes away)

        // A Fish is caught in a Weak Spots round: your click starts it, and only a clean run pays (the auto clicker waits for you).
        if (tier.type == PixelType.Fish && !fishingPass && FishClickStops(tier, tierIndex, clickCount, automatic)) return;

        // Tough pixels (Clicks To Collect > 1) take several clicks; only the last one pays out.
        int breaks = 1; // pixels broken by this click (more than 1 only with the dev click count)
        int need = NeededClicks(tier);                 // a Treasure chest needs a random number of clicks
        int shownNeed = tier.randomClicks && tier.type == PixelType.Treasure ? -1 : need;  // (the chest doesn't tell you how many it needs)
        if (need > 1)
        {
            hitsOnCurrentPixel += clickCount;
            if (hitsOnCurrentPixel < need) { ToughHit(tier, tierIndex, need, shownNeed, automatic); return; }
        }
        if (IsHazard(tier.type)) { TriggerHazard(tierIndex, tier); return; }   // hit one too often: it goes off (no payout)
        if (need > 1)
        {
            SetLiveDamage(tier, need); // the pieces that fall away are fully cracked
            PixelFinalHit?.Invoke(tierIndex, need, automatic);   // the last click shows the final count
        }
        if (need > 1)
        {
            breaks = hitsOnCurrentPixel / need;
            hitsOnCurrentPixel %= need;
        }
        else
        {
            breaks = clickCount;
            hitsOnCurrentPixel = 0;
        }

        double amount = ClickPayout(tier, tierIndex, need, breaks, automatic, out bool richBreak);
        tier.timesCollected += breaks;
        AddCurrency(tierIndex, amount);
        if (amount > 0d) PlayRareFindShow(tier.type, tierIndex);   // a super rare's "you found one" show (its set says which: PixelRareSets)
        PixelCollected?.Invoke(tierIndex, amount, automatic);

        // A Mirror pixel also counts as a click on the last other pixel type collected: it pays that click's amount again.
        if (tier.type == PixelType.Mirror)
        {
            if (IsValidTier(lastReflectTier) && lastReflectAmount > 0d)
            {
                tiers[lastReflectTier].timesCollected += 1;
                AddCurrency(lastReflectTier, lastReflectAmount);
                PixelCollected?.Invoke(lastReflectTier, lastReflectAmount, automatic);
            }
        }
        else
        {
            lastReflectTier = tierIndex;
            lastReflectAmount = amount;
        }

        PlayBreakEffects(tier, tierIndex, need, amount, richBreak, automatic);
        if (tier.vacuum && VacuumThisClick(tierIndex)) Vacuum(tierIndex); // before this pixel's own old copy spawns, so it isn't sucked up too
        if (tier.type == PixelType.Seed) { SeedDigEffect(1f); for (int s = 0, n = UnityEngine.Random.Range(Mathf.Max(1, seedlingsMin), Mathf.Max(Mathf.Max(1, seedlingsMin), seedlingsMax > 0 ? seedlingsMax : 5) + 1); s < n; s++) SpawnSeedSprout(); } // the shell cracked: 1-5 seedlings (they grow plant pixels) come out instead of an old pixel
        else if (spawnFallingCopy)
        {
            aimThisCopy = !automatic;   // (edge-click aiming: a manual click's husk flies towards the side of the cube that was clicked - PixelBurstPop.AimedDirection)
            SpawnFallingCopy(tierIndex, amount);
            aimThisCopy = false;
        }

        // Roll the next pixel AFTER the click so a freshly unlocked tier can appear immediately.
        if (randomizeSpawnTier) currentTierIndex = RollTier();
        Materialize(GetClickTier());

        onPixelClicked?.Invoke();
    }

    /// <summary>CollectInternal step: a click on a Fish (Weak Spots round, or the reel-in clicks without it). True = the click stops here.</summary>
    private bool FishClickStops(PixelTier tier, int tierIndex, int clickCount, bool automatic)
    {
        if (fishingBusy) return true;
        if (PixelWeakspotMinigame.FishingAvailable)
        {
            if (automatic) return true;   // reeling a fish in is up to you
            if (PixelWeakspotMinigame.TryStartFishing(FishingDone)) { fishingBusy = true; PlayClickEffects(tier); }
            return true;
        }
        // The Weak Spots minigame is switched off: a fish takes a random 7-25 clicks to reel in (the last one pays).
        if (fishNeed == 0) fishNeed = UnityEngine.Random.Range(7, 26);
        fishReel += clickCount;
        if (fishReel < fishNeed)
        {
            hitPunchTimer = hitPunchDuration;
            PlayClickEffects(tier);
            if (!disableClickPops) PixelBasicFx.Play(tier.type, tier.color, pixelTransform.position, PixelBaseSize, automatic, 0.35f, clickPopStrength);
            if (!disableFarmFx) PixelFarmFx.Hit(tier.type, pixelTransform.position, PixelBaseSize, tier.color, fishReel / (float)fishNeed);
            PixelHit?.Invoke(tierIndex, fishReel, fishNeed, automatic);
            onPixelClicked?.Invoke();
            return true;
        }
        return false;   // (reeled in: the click goes on and pays)
    }

    /// <summary>CollectInternal step: a click that only damages a tough block (it needs more hits): squash, chips, the block's own hit effects, PixelHit.</summary>
    private void ToughHit(PixelTier tier, int tierIndex, int need, int shownNeed, bool automatic)
    {
        hitPunchTimer = hitPunchDuration;
        SetLiveDamage(tier, hitsOnCurrentPixel);
        PlayClickEffects(tier);
        if (!disableClickPops) PixelBasicFx.Play(tier.type, tier.color, pixelTransform.position, PixelBaseSize, automatic, 0.35f, clickPopStrength);   // a small chip of a tough pixel
        if (tier.type == PixelType.Seed || tier.type == PixelType.Dirt) SeedDigEffect(Mathf.Clamp01(hitsOnCurrentPixel / (float)need));   // digging noises + falling dirt
        if (tier.type != PixelType.Seed && (IsFarm(tier.type) || IsWoodBlock(tier.type) || IsForageBlock(tier.type) || IsFishBlock(tier.type) || tier.type == PixelType.HornetNest || tier.type == PixelType.Toadstool || tier.type == PixelType.Mold || tier.type == PixelType.Jellyfish) && !disableFarmFx) PixelFarmFx.Hit(tier.type, pixelTransform.position, PixelBaseSize, tier.color, hitsOnCurrentPixel / (float)need);
        if (tier.type == PixelType.Dirt) digShrinkTarget = Mathf.Lerp(1f, oldPixelScale, Mathf.Clamp01(hitsOnCurrentPixel / (float)need));   // each dig shrinks it towards old-pixel size
        if (tier.type == PixelType.Pest && !disablePestSwarm) PixelPestSwarm.Scatter(this, pixelTransform.position, 2 + Mathf.RoundToInt(3f * hitsOnCurrentPixel / (float)need));   // bugs spill out of it
        if (tier.type == PixelType.HornetNest && !disableTermites) PixelTermiteSwarm.Scatter(this, pixelTransform.position, 2 + Mathf.RoundToInt(3f * hitsOnCurrentPixel / (float)need));   // termites spill out of the hive
        if ((IsElectricalBlock(tier.type) || tier.type == PixelType.Jellyfish) && !disableFarmFx) PixelElectricFx.Hit(tier.type, pixelTransform.position, PixelBaseSize, hitsOnCurrentPixel / (float)need);
        if (tier.type == PixelType.Obsidian && !disableFarmFx) PixelObsidianFx.Hit(pixelTransform.position, PixelBaseSize, hitsOnCurrentPixel / (float)need);
        if (IsOre(tier.type) && !disableProspectorFx) OreFx.Hit(pixelTransform.position, PixelBaseSize, tier.color, hitsOnCurrentPixel / (float)need);
        if (tier.type == PixelType.Meteor && !disableMeteorFx) PixelCometFx.Hit(pixelTransform.position, PixelBaseSize, hitsOnCurrentPixel / (float)need);
        if (tier.type == PixelType.Treasure && !disableTreasureFx) TreasureFx.Hit(pixelTransform.position, PixelBaseSize, hitsOnCurrentPixel / (float)need);
        if (tier.type == PixelType.Mold) PixelMold.Spread(pixelTransform.position);   // every click spreads the mold further over the fallen pixels
        if (tier.type == PixelType.Whale) PixelWhaleFight.OnHit(this, automatic, hitsOnCurrentPixel, need);   // the first manual hit hooks it: it starts to fight the line
        PixelHit?.Invoke(tierIndex, hitsOnCurrentPixel, shownNeed, automatic);
        onPixelClicked?.Invoke();
    }

    /// <summary>CollectInternal step: what this click pays (the block's own payout rules, random payouts, then every multiplier). richBreak = a rich ore vein broke.</summary>
    private double ClickPayout(PixelTier tier, int tierIndex, int need, int breaks, bool automatic, out bool richBreak)
    {
        double basePayout = tier.amountPerClick * breaks;
        if (transistorBoost > 1d) { basePayout *= transistorBoost; transistorBoost = 1d; }   // a Transistor amplified this click
        if (tier.type == PixelType.Transistor) transistorBoost = transistorMultiplier > 0f ? transistorMultiplier : 2f;
        if (tier.type == PixelType.Fuse)
            basePayout *= Time.time - liveSince <= (fuseSeconds > 0f ? fuseSeconds : 3f) ? (fuseBonus > 0f ? fuseBonus : 3f) : 0.3f;   // collected before it burns out: big, after: almost nothing
        if (tier.type == PixelType.Bulb)
            basePayout *= 1d + CountConductors() * (bulbPerConductor > 0f ? bulbPerConductor : 0.1f);   // it shines brighter the more wired-up pixels lie around
        if (tier.type == PixelType.Capacitor)
        {
            basePayout *= 1d + capacitorCharge * (capacitorPayPerCharge > 0f ? capacitorPayPerCharge : 0.15f);   // it pays out everything the other clicks stored in it
            capacitorCharge = 0;
        }
        else capacitorCharge = Mathf.Min(capacitorChargeCap > 0 ? capacitorChargeCap : 40, capacitorCharge + breaks);
        if (tier.randomClicks && tier.type == PixelType.Treasure) basePayout *= TreasureMultiplier(need);   // the more clicks the chest took, the bigger the reward
        if (tier.randomClicks && tier.type == PixelType.Whale) basePayout *= PixelWhaleFight.RewardMultiplier(need);   // so does a whale: the longer the fight, the bigger the catch
        if (tier.type == PixelType.Wheat) basePayout *= 1d + Mathf.Min(wheatMaxSprouts > 0 ? wheatMaxSprouts : 8, SeedSprout.Count) * (wheatPerSprout > 0f ? wheatPerSprout : 0.25f);   // a busy field feeds the wheat
        richBreak = liveRich && IsOre(tier.type);
        if (richBreak) basePayout *= richVeinMultiplier > 0f ? richVeinMultiplier : 5f;   // a rich vein pays big
        if (tier.randomPayout)
        {
            // A fresh random amount for every pixel harvested (a big dev click count uses the average instead of rolling thousands of times).
            int lo = Mathf.Min(tier.payoutMin, tier.payoutMax), hi = Mathf.Max(tier.payoutMin, tier.payoutMax);
            if (breaks > 50) basePayout = (lo + hi) * 0.5d * breaks;
            else
            {
                basePayout = 0d;
                for (int roll = 0; roll < breaks; roll++) basePayout += UnityEngine.Random.Range(lo, hi + 1);
            }
        }
        return basePayout * PayoutMultiplier(tierIndex) * clickMultiplier * (automatic ? 1d : ManualClickBonus) * PixelBlockRules.HeldPayoutFactor * liveStake;
    }

    /// <summary>CollectInternal step: the effects of a paying click (sound, pops, each block's own break show and on-break action: treasure, fertilizer, bait...).</summary>
    private void PlayBreakEffects(PixelTier tier, int tierIndex, int need, double amount, bool richBreak, bool automatic)
    {
        PlayClickEffects(tier);
        if (!disableClickPops) PixelBasicFx.Play(tier.type, tier.color, pixelTransform.position, PixelBaseSize, automatic, 1f, clickPopStrength);
        if (tier.type == PixelType.Fire && !disableFireFx) FireBurstFx.Play(pixelTransform.position, PixelBaseSize);
        if (tier.type == PixelType.Solar) PixelDaylight.OnSolarClick();
        if (tier.type == PixelType.Treasure) OpenTreasure(tierIndex, need);
        if (tier.type == PixelType.Fertilizer) Fertilize();
        if (tier.type == PixelType.Bait) PixelBait.Drop(this, pixelTransform.position);
        QueueBaitAfter(tier.type);   // (Fishing: a Water pixel may make the next one Bait)
        if (tier.type == PixelType.Bottle) PixelTreasureMap.Begin(this, pixelTransform.position);   // the glass breaks and a treasure map unrolls
        if (tier.type == PixelType.Whale) PixelWhaleFight.OnCaught(this, need, amount);
        if ((IsFarm(tier.type) || IsWoodBlock(tier.type) || IsForageBlock(tier.type) || IsFishBlock(tier.type)) && tier.type != PixelType.Seed && !disableFarmFx) PixelFarmFx.Break(tier.type, pixelTransform.position, PixelBaseSize, tier.color);
        if (tier.type == PixelType.Dirt) PixelAudio.Play("seed_dig");   // the last dig
        if ((IsElectricalBlock(tier.type) || tier.type == PixelType.Jellyfish) && !disableFarmFx) PixelElectricFx.Break(tier.type, pixelTransform.position, PixelBaseSize);
        if (tier.type == PixelType.Obsidian && !disableFarmFx) PixelObsidianFx.Break(pixelTransform.position, PixelBaseSize);
        if (IsOre(tier.type) && !disableProspectorFx)
        {
            OreFx.Break(pixelTransform.position, PixelBaseSize, tier.color, richBreak);
            if (richBreak) { liveRich = false; if (richGlitter != null) { Destroy(richGlitter); richGlitter = null; } }
        }
    }

    /// <summary>The find show of a super rare that was just collected (the set's show in PixelRareSets; nothing for other blocks).</summary>
    private void PlayRareFindShow(PixelType type, int tierIndex)
    {
        PixelRareSets.Set set = PixelRareSets.Of(type);
        if (set == null) return;
        if (set.show == PixelRareSets.Show.GodFind) PixelGodFind.Play(tierIndex, pixelTransform.position);   // each God / Prime Cube has its own show
        else if (set.show == PixelRareSets.Show.DragonFind) PixelDragonFind.Play(tierIndex, pixelTransform.position);
    }
}
