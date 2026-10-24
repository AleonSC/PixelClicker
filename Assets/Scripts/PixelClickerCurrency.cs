// ---- System notes ----
// PixelClickerCurrency.cs
//   Part of PixelClicker (partial class): currency and progress (AddCurrency / AddBonusCurrency / RemoveCurrency / TrySpend, rare-drop announcements, Dragon Cube picks, Ultra pixels, Value / Ultra multipliers, loading the tiers, CheckUnlocks). The system notes are in PixelClicker.cs.
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
    /// <summary>Adds to a tier's amount (use negative via <see cref="TrySpend"/> instead) and checks unlocks.</summary>
    /// <summary>
    /// Currency that does NOT count as earned: it is added to what you hold, but not to the lifetime total that unlocks the next tier (and shows
    /// in the Log). Used by the Recycler, so tier unlocks come from clicking, with recycling as a bonus on top.
    /// </summary>
    public void AddBonusCurrency(int tierIndex, double amount)
    {
        if (!IsValidTier(tierIndex) || amount <= 0 || IsGodCube(tiers[tierIndex].type)) return;
        tiers[tierIndex].count += amount;
        CurrencyGained?.Invoke(tierIndex, amount);
        NotifyChanged();
    }

    public void AddCurrency(int tierIndex, double amount)
    {
        if (!IsValidTier(tierIndex) || amount <= 0) return;
        if (IsGodCube(tiers[tierIndex].type))
        {
            if (tiers[tierIndex].count >= 1d) return;   // one of each God Cube, ever: a second one is never added
            amount = 1d;
        }

        bool firstOfRareDrop = tiers[tierIndex].rareDrop && tiers[tierIndex].totalCollected <= 0d;
        tiers[tierIndex].count += amount;
        tiers[tierIndex].totalCollected += amount;
        CurrencyGained?.Invoke(tierIndex, amount);
        if (firstOfRareDrop) StartCoroutine(AnnounceAfterShow(tierIndex));   // the find show plays first, then the text box

        CheckUnlocks();
        NotifyChanged();
    }

    /// <summary>Waits for the find show (Dragon Cube / God / Prime Cube) to finish, so its text box doesn't pop up on top of the animation.</summary>
    private IEnumerator AnnounceAfterShow(int tierIndex)
    {
        yield return null;   // (the show starts in the same click)
        while (PixelDragonFind.Playing || PixelGodFind.Playing) yield return null;
        AnnounceDragonCube(tierIndex);
    }

    /// <summary>The first time each Dragon Cube turns up: a log line (and a tip the very first time), plus a note when all seven are held.</summary>
    private void AnnounceDragonCube(int tierIndex)
    {
        if (IsInfinityStone(tiers[tierIndex].type))
        {
            PixelHints.Announce("You found the " + tiers[tierIndex].displayName + "! Only one of each Prime Cube can exist.");
            PixelHints.Trigger("stone_first");
            return;
        }
        if (IsGodCube(tiers[tierIndex].type))
        {
            PixelHints.Announce("You found " + tiers[tierIndex].displayName + "! Only one of each God Cube can exist.");
            PixelHints.Trigger("god_first");
            return;
        }
        if (tiers[tierIndex].type == PixelType.GoldenSnitch)
        {
            PixelHints.Announce("Your first Golden Snitch! It is in your Inventory now.");
            return;
        }
        if (tiers[tierIndex].type == PixelType.InfinityCubelette)
        {
            PixelHints.Announce("You forged the Prime Cubelette!");
            return;
        }
        PixelHints.Announce("You found a " + tiers[tierIndex].displayName + "!");
        PixelHints.Trigger("dragon_first");
        if (HasAllDragonCubes) PixelHints.Announce("You hold all seven Dragon Cubes! Summon the dragon.");
    }

    /// <summary>A random Dragon Cube tier the player does not hold right now (so a Dragon Seed grows one they lack). -1 when they hold all seven.</summary>
    public int PickMissingDragonCube()
    {
        System.Collections.Generic.List<int> missing = new System.Collections.Generic.List<int>();
        for (int i = 0; i < tiers.Length; i++)
            if (tiers[i].rareDrop && IsDragonCube(tiers[i].type) && tiers[i].count < 1d) missing.Add(i);
        return missing.Count == 0 ? -1 : missing[UnityEngine.Random.Range(0, missing.Count)];
    }

    /// <summary>Any one of the seven Dragon Cube tiers at random (-1 if they don't exist). Used by the Dragon Seed under Infinite resources.</summary>
    public int PickAnyDragonCube()
    {
        System.Collections.Generic.List<int> all = new System.Collections.Generic.List<int>();
        for (int i = 0; i < tiers.Length; i++) if (tiers[i].rareDrop && IsDragonCube(tiers[i].type)) all.Add(i);
        return all.Count == 0 ? -1 : all[UnityEngine.Random.Range(0, all.Count)];
    }

    /// <summary>Does the player hold at least one of each of the seven Dragon Cubes?</summary>
    public bool HasAllDragonCubes
    {
        get
        {
            for (int s = 0; s < 7; s++)
            {
                int index = IndexOf((PixelType)((int)PixelType.DragonCube1 + s));
                if (index < 0 || tiers[index].count < 1d) return false;
            }
            return true;
        }
    }

    /// <summary>Takes up to 'amount' off a tier's currency (never below zero) without counting it as spending. Returns how much was taken.</summary>
    public double RemoveCurrency(int tierIndex, double amount)
    {
        if (!IsValidTier(tierIndex) || amount <= 0) return 0d;
        double taken = System.Math.Min(tiers[tierIndex].count, amount);
        if (taken <= 0d) return 0d;
        tiers[tierIndex].count -= taken;
        NotifyChanged();
        return taken;
    }

    // ---- progress: the Ultra pixels (held, ever earned) live in the GameState (PixelGameState.Current.clicker), like every tier's counts ----
    private static ClickerState CS => PixelGameState.Current.clicker;
    private static long ultraEarned { get => CS.ultraEarned; set => CS.ultraEarned = value; }

    /// <summary>Ultra pixels ever earned (spending them on boosts does not lower this). The Ultra Pad's goal counter uses it.</summary>
    public long UltraEarned => ultraEarned;

    /// <summary>Sets the lifetime Ultra count (never below what you currently hold): the Ultra Holes' goal counter / the Dev Hub.</summary>
    public void SetUltraEarned(long value) => ultraEarned = System.Math.Max(System.Math.Max(0L, value), TotalUltra);

    // Ultra pixels are ONE general currency now (not per pixel type): minigames reward them, upgrades will spend them.
    private static long ultraPixels { get => CS.ultraHeld; set => CS.ultraHeld = value; }

    /// <summary>The shared Ultra roll used by the minigames: a 'chance' (0..1) of giving one Ultra pixel. Returns true if it did.</summary>
    public bool RollUltra(float chance)
    {
        if (chance <= 0f || UnityEngine.Random.value >= chance) return false;
        AddUltra(1);
        return true;
    }

    /// <summary>Gives Ultra pixels.</summary>
    public void AddUltra(long amount)
    {
        if (amount <= 0) return;
        ultraEarned += amount;
        ultraPixels += amount;
        NotifyChanged();
        UltraGained?.Invoke();
    }


    /// <summary>How much each Ultra boost level adds to a pixel type's payout multiplier.</summary>
    public float UltraBonusPerLevel => ultraBonusPerLevel;

    /// <summary>Is this a valid index into the tier list?</summary>
    public bool IsValidTierIndex(int index) => IsValidTier(index);

    /// <summary>A pixel type's payout multiplier from its Ultra boost level (1 = no boost).</summary>
    public double UltraMultiplier(int tierIndex) =>
        IsValidTier(tierIndex) ? 1d + tiers[tierIndex].ultraLevel * (double)ultraBonusPerLevel : 1d;

    /// <summary>Payout multiplier of a pixel type at a given Value level (1 at level 0).</summary>
    public double ValueMultiplierAt(int level)
    {
        level = System.Math.Max(0, level);
        double m = 1d + level * (double)valueBonusPerLevel;
        if (valueMilestoneEvery > 0) m *= System.Math.Pow(valueMilestoneMultiplier, level / valueMilestoneEvery);
        return m;
    }

    /// <summary>A pixel type's payout multiplier from its Value upgrade level (1 = no upgrade).</summary>
    public double ValueMultiplier(int tierIndex) => IsValidTier(tierIndex) ? ValueMultiplierAt(tiers[tierIndex].valueLevel) : 1d;

    /// <summary>Everything that scales a pixel type's payout per collect: Ultra boosts x Value upgrades.</summary>
    public double PayoutMultiplier(int tierIndex) => UltraMultiplier(tierIndex) * ValueMultiplier(tierIndex) * WishMultiplier;

    /// <summary>Spends a pixel type's own currency to raise its Value level by one. Returns false if you can't afford it.</summary>
    public bool TryBuyValueLevel(int tierIndex, double cost)
    {
        if (!IsValidTier(tierIndex) || !TrySpend(tierIndex, cost)) return false;
        tiers[tierIndex].valueLevel++;
        NotifyChanged();
        return true;
    }

    /// <summary>Spends Ultra pixels to raise a pixel type's boost level by one. Returns false if you don't have enough.</summary>
    public bool TryBuyUltraBoost(int tierIndex, long cost)
    {
        if (!IsValidTier(tierIndex) || cost < 0 || (!InfiniteResources && ultraPixels < cost)) return false;
        if (!InfiniteResources) ultraPixels -= cost;
        tiers[tierIndex].ultraLevel++;
        NotifyChanged();
        return true;
    }

    /// <summary>Ultra pixels held.</summary>
    public long TotalUltra => ultraPixels;

    /// <summary>Can the player pay this much of a pixel type? (Always, with the infinite resources cheat.)</summary>
    public bool CanAfford(PixelType type, double amount) => InfiniteResources || GetCount(type) >= amount;

    /// <summary>Spends currency if the player can afford it. Use this from shops later.</summary>
    public bool TrySpend(int tierIndex, double amount)
    {
        if (InfiniteResources) return IsValidTier(tierIndex) && amount >= 0;
        if (!IsValidTier(tierIndex) || amount < 0 || tiers[tierIndex].count < amount) return false;
        tiers[tierIndex].count -= amount;
        if (amount > 0d) CurrencySpent?.Invoke(tierIndex, amount);
        NotifyChanged();
        return true;
    }

    /// <summary>Convenience overload using the enum.</summary>
    public bool TrySpend(PixelType type, double amount) => TrySpend(IndexOf(type), amount);

    /// <summary>Current spendable amount of a currency.</summary>
    public double GetCount(PixelType type)
    {
        int i = IndexOf(type);
        return i >= 0 ? tiers[i].count : 0;
    }

    /// <summary>Manually select which tier clicks produce (only used when Auto Use Highest Tier is off).</summary>
    public void SetActiveTier(int index)
    {
        if (!IsValidTier(index) || !tiers[index].unlocked) return;
        activeTierIndex = index;
        ApplyTierLook(tiers[index]);
        RefreshUI();
    }

    /// <summary>Every tier of the clicker keeps its progress in the GameState (call after the tier list changed; cheap when nothing is new).</summary>
    private void MakeTiersLive()
    {
        if (tiers == null) return;
        for (int i = 0; i < tiers.Length; i++) if (tiers[i] != null && !tiers[i].IsLive) tiers[i].MakeLive();
    }

    /// <summary>
    /// A save was loaded (PixelSaveGame calls this at its old fixed step, FIRST: the shop and everything after it read the currencies): the tiers
    /// read the loaded GameState from now on (they bind to it by themselves); here the start-unlocked ones stay unlocked and Ultra ever earned is never
    /// below Ultra held. Then FinishLoad, as before.
    /// </summary>
    public void ApplyLoadedTiers()
    {
        MakeTiersLive();
        for (int i = 0; i < tiers.Length; i++) if (tiers[i].unlockedAtStart) tiers[i].unlocked = true;
        ultraEarned = System.Math.Max(ultraEarned, ultraPixels);
        NotifyChanged();
    }

    /// <summary>Adds to a tier's "times collected" (used for offline progress, which pays without clicking).</summary>
    public void AddTimesCollected(int tierIndex, long count)
    {
        if (IsValidTier(tierIndex) && count > 0) tiers[tierIndex].timesCollected += count;
    }

    /// <summary>Switches a pixel type's spawning on or off (only pixels that can be switched off). Re-rolls the next pixel if needed.</summary>
    public void SetSpawnEnabled(int tierIndex, bool enabled)
    {
        if (!IsValidTier(tierIndex) || !tiers[tierIndex].CanSwitchOff) return;
        tiers[tierIndex].spawnDisabled = !enabled;
        if (randomizeSpawnTier && !enabled && currentTierIndex == tierIndex && !isSpawning)
        {
            currentTierIndex = RollTier();
            Materialize(GetClickTier());
        }
    }

    /// <summary>Call after <see cref="ApplyLoadedTiers"/>: re-rolls the pixel on screen and refreshes every display.</summary>
    public void FinishLoad()
    {
        ClearForcedSpawnTier();
        if (randomizeSpawnTier) currentTierIndex = RollTier();
        Materialize(GetClickTier());
        NotifyChanged();
    }

    /// <summary>Unlock tier N once tier N-1's lifetime total reaches N's threshold.</summary>
    private void CheckUnlocks()
    {
        for (int i = 1; i < tiers.Length; i++)
        {
            if (tiers[i].unlocked) continue;
            if (tiers[i].unlockMode == TierUnlockMode.ShopOnly) continue;
            if (tiers[i].unlockMode == TierUnlockMode.ByGoal && PixelGoals.UnlocksTier(tiers[i].type)) continue;   // a goal's reward unlocks it (PixelGoals), not the threshold
            if (tiers[i - 1].totalCollected >= tiers[i].unlockThreshold)
            {
                tiers[i].unlocked = true;
                PlaySound(unlockSound);
                onTierUnlocked?.Invoke(i);
            }
        }
    }
}
