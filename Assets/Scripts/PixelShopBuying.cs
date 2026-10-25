// ---- System notes ----
// PixelShopBuying.cs
//   Part of PixelShop (partial class): pack state, requirements, costs, buying and the dev unlocks. The system notes are in PixelShop.cs.
// ---------------------------------------------------
using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public partial class PixelShop
{
    [Header("Texts (unlocks)")]
    [Tooltip("Event log line when a pack unlocks Glass pixels.")] [SerializeField] private string glassUnlockedText = "You unlocked Glass Pixels!";
    [Tooltip("Event log line when a pack unlocks the Auto Clicker.")] [SerializeField] private string autoClickerUnlockedText = "You unlocked the Auto Clicker!";

    private const string glassToggleHint = "Glass unlocked!\nIt is switched ON by default and now spawns with your other pixels. Open the Toggles window (the tab on the right edge of the screen), go to Pixels and untick Glass if you don't want it. Crafting is unlocked too!";

    private static bool IsLeveled(ShopPack pack) => pack.levels != null && pack.levels.Length > 0;

    /// <summary>The costs for the pack's next purchase (the next level for upgrade packs). Null if nothing left to buy.</summary>
    private PackCost[] CurrentCosts(ShopPack pack)
    {
        if (pack.skillLadder) return LadderCosts(pack);
        if (!IsLeveled(pack)) return pack.costs;
        return pack.level < pack.levels.Length ? pack.levels[Mathf.Max(0, pack.level)].costs : null;
    }

    // ------------------------------------------------------------------
    // Skill pack price ladder (GameDesign section 6 'Tier 0'): every gathering pack is on sale from the start, each one dearer than the last.
    // ------------------------------------------------------------------

    /// <summary>
    /// The price of a gathering skill pack (ShopPack.skillLadder): skillLadderStarterPrice of each of the six pure colours x skillLadderGrowth ^ rung,
    /// rounded to two significant figures. The rung: by purchase order, how many ladder packs you already own (the Nth pack you buy costs rung N-1,
    /// whichever it is); or, with that off, the pack's place among the ladder packs in the list. With skillLadderStarterFixed the Lumberjack's
    /// Pack always costs rung 0 and every other pack at least rung 1, so Woodcutting stays the cheap first skill.
    /// </summary>
    private PackCost[] LadderCosts(ShopPack pack)
    {
        int rung = 0;
        bool starter = skillLadderStarterFixed && IsWoodcutterPack(pack);
        if (!starter)
        {
            for (int i = 0; i < packs.Length; i++)
            {
                ShopPack p = packs[i];
                if (p == pack && !skillLadderByPurchaseOrder) break;   // fixed rungs: count the ladder packs listed before this one
                if (p == null || p == pack || !p.skillLadder) continue;
                if (!skillLadderByPurchaseOrder || IsPurchased(i)) rung++;   // by purchase order: count the ones already bought
            }
            if (skillLadderStarterFixed) rung = Mathf.Max(1, rung);
        }
        return AllSix(LadderPrice(rung));
    }

    /// <summary>Price per pure colour of ladder rung 'rung' (0 = the starter).</summary>
    public double LadderPrice(int rung)
    {
        double raw = skillLadderStarterPrice * System.Math.Pow(System.Math.Max(1d, skillLadderGrowth), rung);
        if (raw <= 0d) return 0d;
        double scale = System.Math.Pow(10d, System.Math.Floor(System.Math.Log10(raw)) - 1d);   // (two significant figures: 625 -> 630, 1563 -> 1600)
        return System.Math.Round(raw / scale) * scale;
    }

    /// <summary>One-time packs: bought. Upgrade packs: at max level (nothing left to buy).</summary>
    // --- Save / load access (used by PixelSaveGame) ---

    public int PackCount => packs.Length;

    /// <summary>Every pack, read-only (the game database reads it).</summary>
    public System.Collections.Generic.IReadOnlyList<ShopPack> AllPacks => packs;

    public string GetPackName(int index) => packs[index].displayName;

    public bool GetPackPurchased(int index) => packs[index].purchased;

    public int GetPackLevel(int index) => packs[index].level;

    /// <summary>
    /// A save was loaded (PixelSaveGame calls this at its old fixed step, right after the tiers - pack effects must be switched on / off before
    /// the rest of the load): every pack takes its bought flag and level from the GameState and its side effects follow (SetPackState).
    /// </summary>
    public void ApplyLoadedPackStates()
    {
        for (int i = 0; i < packs.Length; i++)
            if (packs[i] != null) SetPackState(i, packs[i].purchased, packs[i].level);
    }

    /// <summary>Sets a pack's bought flag and level without re-applying its effect (switches off what a not-bought pack gives; re-applies the upgrades stored nowhere else).</summary>
    public void SetPackState(int index, bool purchased, int level)
    {
        ShopPack pack = packs[index];
        pack.purchased = purchased;
        pack.level = IsLeveled(pack) ? Mathf.Clamp(level, 0, pack.levels.Length) : 0;
        pack.appliedLevel = pack.level;
        if (pack.upgradeEffect == UpgradeEffect.ComboMeter || pack.upgradeEffect == UpgradeEffect.BankCapacity) ApplyUpgrade(pack); // not stored anywhere else, so re-apply on load

        if (pack.unlocksCrafting && crafting != null && !purchased) crafting.Deactivate();
        if (pack.unlocksTimeStop && timeStop != null && !purchased) timeStop.Deactivate();
        if (pack.unlocksTimeSlow && timeStop != null && !purchased) timeStop.DeactivateSlow();
        if (pack.unlocksBank && bank != null && !purchased) bank.Deactivate();
        if (pack.unlocksRobot && robot != null && !purchased) robot.Deactivate();
        if (pack.unlocksBlockHold && !purchased) PixelFind.Cached<PixelBlockHold>()?.Deactivate();
        if (!purchased && !string.IsNullOrEmpty(pack.unlocksMinigame)) PixelMinigame.Find(pack.unlocksMinigame)?.Deactivate();
    }

    /// <summary>The potions this shop sells (also used by the inventory UI so both see the same stock).</summary>
    public PixelConsumables Consumables => consumables;

    /// <summary>Tree mode (PixelTreeMode): is a pack with unlocksTreeMode owned?</summary>
    public bool TreeModeOwned()
    {
        if (packs == null) return false;
        foreach (ShopPack p in packs) if (p != null && p.unlocksTreeMode && p.purchased) return true;
        return false;
    }

    /// <summary>Has the shop pack that unlocks this skill (by <see cref="ShopPack.skillId"/>) been bought?</summary>
    public bool IsSkillPackBought(string skillId)
    {
        for (int i = 0; i < packs.Length; i++)
            if (packs[i] != null && packs[i].skillId == skillId && IsPurchased(i)) return true;
        return false;
    }

    public bool IsPurchased(int packIndex)
    {
        ShopPack pack = packs[packIndex];
        if (IsLeveled(pack)) return pack.level >= pack.levels.Length;
        if (pack.purchased) return true;
        if (pack.rewardTiers == null || pack.rewardTiers.Length == 0) return false;
        foreach (PixelClicker.PixelTier reward in pack.rewardTiers)
            if (!clicker.IsUnlocked(reward.type)) return false;
        return true;
    }

    /// <summary>Owned at all: bought (one-time packs) or at least level 1 (upgrade packs).</summary>
    private bool HasPack(int packIndex)
    {
        ShopPack pack = packs[packIndex];
        return IsLeveled(pack) ? pack.level > 0 : IsPurchased(packIndex);
    }

    /// <summary>True when the pack's required pack (if any) has been bought.</summary>
    public bool IsRequirementMet(int packIndex) => IsPackRequirementMet(packIndex) && IsMinigameGoalMet(packIndex) && IsDiscoveryMet(packIndex) && PixelCubieBill.TierMet(PixelCubieBill.PackTier(packs[packIndex]));

    // ---- MACHINE DISCOVERY (RequirementKind.Discovery, 2026-10-25): 'a machine has made N of something' (a PixelStats counter). ----
    // The pack is listed but locked ('Discover Charcoal first: 14 / 50 made') until the counter reaches its target; the counter never
    // goes down, so it is a one-time discovery. A machine calls NoteDiscovery(statKey) after raising its counter (the notice), and its
    // device card shows DiscoveryProgress(statKey, ...). Built for the Kiln's Charcoal; the next making skill's blocks reuse it.

    /// <summary>True unless the pack waits on a machine discovery that has not been reached.</summary>
    public bool IsDiscoveryMet(int packIndex)
    {
        PackRequirement[] requirements = packs[packIndex].requirements;
        if (requirements == null) return true;
        foreach (PackRequirement r in requirements)
            if (r.kind == RequirementKind.Discovery && !IsDiscovered(r)) return false;
        return true;
    }

    private static bool IsDiscovered(PackRequirement r) => string.IsNullOrEmpty(r.statKey) || PixelStats.Total(r.statKey) >= r.count;

    /// <summary>The discovery requirement on any pack that watches this counter (and that pack's index), or null.</summary>
    private PackRequirement DiscoveryFor(string statKey, out int packIndex)
    {
        packIndex = -1;
        if (packs == null || string.IsNullOrEmpty(statKey)) return null;
        for (int i = 0; i < packs.Length; i++)
        {
            if (packs[i] == null || packs[i].requirements == null) continue;
            foreach (PackRequirement r in packs[i].requirements)
                if (r != null && r.kind == RequirementKind.Discovery && r.statKey == statKey) { packIndex = i; return r; }
        }
        return null;
    }

    /// <summary>A machine's discovery progress for its device card: made so far, needed, what it discovers. False when no pack watches this counter.</summary>
    public static bool DiscoveryProgress(string statKey, out double have, out double need, out string label)
    {
        have = PixelStats.Total(statKey); need = 0d; label = "";
        PixelShop shop = PixelFind.Cached<PixelShop>();
        PackRequirement r = shop != null ? shop.DiscoveryFor(statKey, out _) : null;
        if (r == null) return false;
        need = r.count;
        label = string.IsNullOrEmpty(r.label) ? statKey : r.label;
        return true;
    }

    /// <summary>A machine raised its discovery counter by 'added': when that just reached the target, the notice ('You've discovered Charcoal! ...').</summary>
    public static void NoteDiscovery(string statKey, double added = 1d)
    {
        PixelShop shop = PixelFind.Cached<PixelShop>();
        int packIndex = -1;
        PackRequirement r = shop != null ? shop.DiscoveryFor(statKey, out packIndex) : null;
        if (r == null || packIndex < 0) return;
        double now = PixelStats.Total(statKey);
        if (now < r.count || now - added >= r.count) return;   // (not there yet, or reached before)
        string label = string.IsNullOrEmpty(r.label) ? statKey : r.label;
        PixelHints.Announce(string.Format(shop.discoveredFormat, label, shop.packs[packIndex].displayName));
        PixelAudio.Play("achievement");
    }

    /// <summary>True when every pack this pack requires (if any) has been bought.</summary>
    public bool IsPackRequirementMet(int packIndex)
    {
        if (!PixelCubieBill.TierListed(PixelCubieBill.PackTier(packs[packIndex]))) return false;   // (Cubie's trust tier: hidden until his bill is revealed, then listed as locked)
        PackRequirement[] requirements = packs[packIndex].requirements;
        if (requirements == null) return true;

        foreach (PackRequirement r in requirements)
            if (r.kind == RequirementKind.Pack && IsRequiredPackMissing(r, packIndex)) return false;
        return true;
    }

    /// <summary>True unless the pack needs a minigame's goal (ghosts caught, singularity...) and it has not been reached.</summary>
    public bool IsMinigameGoalMet(int packIndex)
    {
        PackRequirement[] requirements = packs[packIndex].requirements;
        if (requirements == null) return true;

        foreach (PackRequirement r in requirements)
            if (r.kind == RequirementKind.MinigameGoal && !IsGoalReached(r)) return false;
        return true;
    }

    private bool IsRequiredPackMissing(PackRequirement r, int ownIndex)
    {
        if (r.packIndex < 0 || r.packIndex >= packs.Length || r.packIndex == ownIndex) return false;
        return !HasPack(r.packIndex);
    }

    private static bool IsGoalReached(PackRequirement r)
    {
        PixelMinigame minigame = PixelMinigame.Find(r.minigameId);
        return minigame != null && minigame.GoalReached;
    }

    /// <summary>The text for the first requirement that is not met yet ("Requires: ...").</summary>
    private string RequirementText(int packIndex)
    {
        int trustTier = PixelCubieBill.PackTier(packs[packIndex]);
        if (!PixelCubieBill.TierMet(trustTier)) return PixelCubieBill.LockedText(trustTier);
        PackRequirement[] requirements = packs[packIndex].requirements;
        if (requirements == null) return "";

        foreach (PackRequirement r in requirements)
        {
            if (r.kind == RequirementKind.Pack)
            {
                if (IsRequiredPackMissing(r, packIndex)) return string.Format(requiresFormat, packs[r.packIndex].displayName);
            }
            else if (r.kind == RequirementKind.Discovery)
            {
                if (!IsDiscovered(r)) return string.Format(discoveryRequirementFormat, string.IsNullOrEmpty(r.label) ? r.statKey : r.label,
                                                           PixelClicker.FormatNumberShort(System.Math.Min(r.count, PixelStats.Total(r.statKey))), PixelClicker.FormatNumberShort(r.count));
            }
            else if (!IsGoalReached(r))
            {
                PixelMinigame minigame = PixelMinigame.Find(r.minigameId);
                if (minigame == null) return "";
                return string.Format(minigame.RequirementFormat, PixelClicker.FormatNumberShort(minigame.TrackerCount),
                                     PixelClicker.FormatNumberShort(minigame.TrackerGoal));
            }
        }
        return "";
    }

    /// <summary>True when the player holds enough of every currency for the pack's next purchase.</summary>
    public bool CanAfford(int packIndex)
    {
        return CanAffordCosts(CurrentCosts(packs[packIndex]));
    }

    private bool CanAffordCosts(PackCost[] costs)
    {
        if (costs == null) return true;
        foreach (PackCost cost in costs)
            if (!CanAffordCost(cost)) return false;
        return true;
    }

    /// <summary>Can the player pay this one line of a price (a pixel type, a material like Charcoal, or a minigame counter like bomb parts)?</summary>
    private bool CanAffordCost(PackCost cost)
    {
        if (string.IsNullOrEmpty(cost.minigameCurrency)) return clicker.CanAfford(cost.type, cost.amount);
        return PixelMaterials.CostAffordable(cost.minigameCurrency, cost.amount);
    }

    /// <summary>Pays one line of a price.</summary>
    private void SpendCost(PackCost cost)
    {
        if (string.IsNullOrEmpty(cost.minigameCurrency)) { clicker.TrySpend(cost.type, cost.amount); return; }
        PixelMaterials.CostSpend(cost.minigameCurrency, cost.amount);
    }

    /// <summary>True once the Shop button is showing (its required pixel is unlocked).</summary>
    public bool ShopAvailable => clicker != null && (alwaysShowButton || clicker.IsUnlocked(requiredTier));

    /// <summary>
    /// The Log's Goals tab: progress towards buying the Auto Clicker (shown once Red, Green and Blue are unlocked) - 'have' / 'need' add up every price line
    /// (each line counts at most its own price), 'costText' lists the price. False if there is no such pack or it is already bought.
    /// </summary>
    public bool TryGetAutoClickerGoal(out string packName, out string costText, out double have, out double need)
        => TryGetPackGoal(p => p.unlocksAutoClicker, false, out packName, out costText, out have, out need);

    /// <summary>The Lumberjack's Pack goal (shown after the Toggles tutorial's skills hint): false once it is bought.</summary>
    public bool TryGetWoodcutterGoal(out string packName, out string costText, out double have, out double need)
        => TryGetPackGoal(p => Rewards(p, PixelClicker.PixelType.Wood), false, out packName, out costText, out have, out need);

    /// <summary>Is the Auto Clicker pack bought?</summary>
    public bool AutoClickerBought()
    {
        if (packs == null) return false;
        for (int i = 0; i < packs.Length; i++) if (packs[i] != null && packs[i].unlocksAutoClicker) return IsPurchased(i);
        return false;
    }

    private bool TryGetPackGoal(Predicate<ShopPack> match, bool firstLevelOnly, out string packName, out string costText, out double have, out double need)
    {
        packName = costText = "";
        have = need = 0d;
        if (clicker == null || packs == null) return false;
        for (int i = 0; i < packs.Length; i++)
        {
            ShopPack pack = packs[i];
            if (pack == null || !match(pack)) continue;
            if (firstLevelOnly ? pack.level >= 1 : IsPurchased(i)) return false;
            packName = pack.displayName;
            PackCost[] costs = CurrentCosts(pack);
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            if (costs != null)
            {
                foreach (PackCost cost in costs)
                {
                    double held = string.IsNullOrEmpty(cost.minigameCurrency)
                        ? clicker.GetCount(cost.type)
                        : PixelMaterials.CostHeld(cost.minigameCurrency);
                    if (PixelClicker.InfiniteResources) held = cost.amount;
                    have += Math.Min(Math.Max(0d, held), cost.amount);
                    need += cost.amount;
                    if (sb.Length > 0) sb.Append(", ");
                    sb.Append(PixelClicker.FormatNumberShort(cost.amount)).Append(' ').Append(CostName(cost));
                }
            }
            costText = sb.ToString();
            if (need <= 0d) need = 1d;
            return true;
        }
        return false;
    }

    /// <summary>Name of what a price line is paid in (a pixel's name, a material's name, or the minigame counter's title).</summary>
    private string CostName(PackCost cost)
    {
        if (!string.IsNullOrEmpty(cost.minigameCurrency)) return PixelMaterials.CostName(cost.minigameCurrency);
        int tierIndex = clicker.IndexOf(cost.type);
        return tierIndex >= 0 ? clicker.Tiers[tierIndex].displayName : cost.type.ToString();
    }

    /// <summary>Buys one of a potion / device (see <see cref="TryBuyItems"/>).</summary>
    public bool TryBuyPotion(int itemIndex) => TryBuyItems(itemIndex, 1);

    /// <summary>Buys several of a potion / device at once. Returns false if it is locked or you can't afford all of them.</summary>
    public bool TryBuyItems(int itemIndex, int count)
    {
        if (consumables == null || itemIndex < 0 || itemIndex >= consumables.ItemCount || count < 1) return false;
        if (consumables.ItemCraftOnly(itemIndex) || consumables.ItemNotSold(itemIndex) || consumables.ItemVendorOnly(itemIndex) || ItemLocked(itemIndex)) return false;
        if (count > consumables.ItemRoom(itemIndex)) return false; // potions are capped per type

        PackCost[] total = consumables.ItemCostsTotal(itemIndex, count);   // (a rising price adds up step by step)
        if (!CanAffordCosts(total)) return false;

        foreach (PackCost cost in total) SpendCost(cost);

        consumables.AddItem(itemIndex, count);
        PixelStats.Count("shop.items", count);
        PixelDebug.Info("PixelShop: bought " + count + " x " + consumables.ItemName(itemIndex) + " - you now own " +
                  consumables.ItemOwned(itemIndex) + ".", this);
        PlayPurchaseSound();
        RefreshRows();
        return true;
    }


    /// <summary>Sets the auto clicker's interval / clicks to the value of the pack's current level.</summary>
    private void ApplyUpgrade(ShopPack pack)
    {
        pack.level = Mathf.Clamp(pack.level, 0, pack.levels.Length);
        pack.appliedLevel = pack.level;

        if (pack.upgradeEffect == UpgradeEffect.ComboMeter)
        {
            if (combo != null) combo.SetUpgrade(pack.level > 0, pack.level > 0 ? pack.levels[pack.level - 1].value : 0f);
            return;
        }
        if (pack.upgradeEffect == UpgradeEffect.BankCapacity)
        {
            if (bank != null) bank.SetCapacityOverride(pack.level > 0 ? Mathf.RoundToInt(pack.levels[pack.level - 1].value) : 0);
            return;
        }
        if (pack.level <= 0 || autoClicker == null) return;

        float value = pack.levels[pack.level - 1].value;
        switch (pack.upgradeEffect)
        {
            case UpgradeEffect.AutoClickerInterval: autoClicker.Interval = value; break;
            case UpgradeEffect.AutoClickerClicks: autoClicker.ClicksPerTick = Mathf.RoundToInt(value); break;
        }
    }

    /// <summary>Unlocks a pack's reward tiers and starts the auto clicker if it has one. Safe to call repeatedly.</summary>
    private void ApplyPackEffects(ShopPack pack)
    {
        if (pack.rewardTiers != null)
        {
            foreach (PixelClicker.PixelTier reward in pack.rewardTiers)
            {
                int index = clicker.IndexOf(reward.type);
                if (index >= 0) clicker.UnlockTier(index); // no-op if already unlocked
            }
        }

        if (pack.unlocksAutoClicker && autoClicker != null) autoClicker.Activate(); // no-op if already running
        if (pack.unlocksCrafting && crafting != null) crafting.Activate();
        if (pack.unlocksGrabbing && grab != null) grab.Activate();
        if (pack.unlocksTimeStop && timeStop != null) timeStop.Activate();
        if (pack.unlocksTimeSlow && timeStop != null) timeStop.ActivateSlow();
        if (pack.unlocksBank && bank != null) bank.Activate();
        if (pack.unlocksRobot && robot != null) robot.Activate();
        if (pack.unlocksBlockHold) PixelFind.Cached<PixelBlockHold>()?.Activate();
        if (!string.IsNullOrEmpty(pack.unlocksMinigame)) ActivateMinigame(pack.unlocksMinigame); // no-op if already running
    }

    /// <summary>Ultra pixels the next boost level of a pixel type costs.</summary>
    public long UltraBoostCost(int tierIndex) =>
        ultraBaseCost + (clicker.IsValidTierIndex(tierIndex) ? clicker.Tiers[tierIndex].ultraLevel : 0) * ultraCostPerLevel;

    /// <summary>True when a pixel type has reached its highest boost level.</summary>
    public bool UltraBoostMaxed(int tierIndex) =>
        ultraMaxLevel > 0 && clicker.IsValidTierIndex(tierIndex) && clicker.Tiers[tierIndex].ultraLevel >= ultraMaxLevel;

    /// <summary>Spends Ultra pixels to boost a pixel type's payout. Returns false if it is maxed or you can't afford it.</summary>
    public bool TryBuyUltraBoost(int tierIndex)
    {
        if (UltraBoostMaxed(tierIndex)) return false;
        bool ok = clicker.TryBuyUltraBoost(tierIndex, UltraBoostCost(tierIndex));
        if (ok)
        {
            PlayPurchaseSound();
            if (clicker.IsValidTierIndex(tierIndex))
            {
                string boostLine = "Ultra boost bought: " + clicker.Tiers[tierIndex].displayName + " level " + clicker.Tiers[tierIndex].ultraLevel + "!";
                PixelHints.Announce(boostLine, boostLine);
            }
            PixelHints.Trigger("upgrade_ultra");
        }
        return ok;
    }

    /// <summary>Currency (of the pixel type itself) the next Value level of a pixel type costs.</summary>
    public double ValueUpgradeCost(int tierIndex) =>
        clicker.IsValidTierIndex(tierIndex) ? ValueCostAtLevel(tierIndex, clicker.Tiers[tierIndex].valueLevel) : 0d;

    /// <summary>What the Value level after 'level' costs (the shop's amount buttons add several up).</summary>
    private double ValueCostAtLevel(int tierIndex, int level)
    {
        if (!clicker.IsValidTierIndex(tierIndex)) return 0d;
        PixelClicker.PixelTier tier = clicker.Tiers[tierIndex];
        // Scaled by the pixel's payout per collect but NOT by how often it spawns, so rarer pixels are deliberately slower to upgrade.
        return Math.Ceiling(valueBaseCost * Math.Max(1d, tier.amountPerClick) * Math.Pow(valueCostGrowth, level));
    }

    /// <summary>True when a pixel type has reached its highest Value level (the absolute limit, valueMaxLevel).</summary>
    public bool ValueUpgradeMaxed(int tierIndex) =>
        valueMaxLevel > 0 && clicker.IsValidTierIndex(tierIndex) && clicker.Tiers[tierIndex].valueLevel >= valueMaxLevel;

    /// <summary>
    /// True when the owning skill's level stops the next Value level (Value level >= skill level x valueLevelsPerSkillLevel). 'skillName' and
    /// 'needLevel' say which skill and level the next Value level needs. False with the cap off or no skill system.
    /// </summary>
    public bool ValueSkillCapped(int tierIndex, out string skillName, out int needLevel)
    {
        skillName = ""; needLevel = 0;
        if (!valueCapBySkill || !clicker.IsValidTierIndex(tierIndex)) return false;
        if (!PixelSkills.TryGetOwnerLevel(clicker.Tiers[tierIndex].type, out skillName, out int level)) return false;
        int per = Mathf.Max(1, valueLevelsPerSkillLevel);
        int value = clicker.Tiers[tierIndex].valueLevel;
        if (value < level * per) return false;
        needLevel = value / per + 1;
        return true;
    }

    /// <summary>Can the next Value level be bought at all (not maxed, not held back by the skill cap)? Cost aside.</summary>
    private bool ValueLevelOpen(int tierIndex) => !ValueUpgradeMaxed(tierIndex) && !ValueSkillCapped(tierIndex, out _, out _);

    /// <summary>Buys one Value level (or, with Shift held, as many as you can afford). Returns how many were bought.</summary>
    public int TryBuyValueUpgrade(int tierIndex, bool bulk)
    {
        int bought = 0;
        int limit = bulk ? Mathf.Max(1, valueMaxBulk) : 1;
        while (bought < limit && ValueLevelOpen(tierIndex) && clicker.TryBuyValueLevel(tierIndex, ValueUpgradeCost(tierIndex)))
            bought++;
        if (bought > 0)
        {
            PlayPurchaseSound(); // no event-log line: Value upgrades are bought so often that it was spam
        }
        return bought;
    }

    /// <summary>Starts a minigame unless the player switched it off.</summary>
    private static void ActivateMinigame(string id)
    {
        PixelMinigame m = PixelMinigame.Find(id);
        if (m != null && !m.UserDisabled) m.Activate();
    }

    /// <summary>Dev tools (the Unlock All button): unlocks every pack for free (leveled packs go to their top level), every ordinary pixel type, and gives every skill its maximum XP. Ignores requirements and costs.</summary>
    public void DevUnlockAll()
    {
        for (int t = 0; t < clicker.Tiers.Length; t++)
        {
            PixelClicker.PixelTier tier = clicker.Tiers[t];
            if (tier.rareDrop || PixelClicker.IsHazard(tier.type) || PixelClicker.IsRetiredOre(tier.type)) continue;   // (collectibles are found, hazards are not wanted)
            clicker.UnlockTier(t);
        }
        for (int i = 0; i < packs.Length; i++)
        {
            ShopPack pack = packs[i];
            if (IsLeveled(pack))
            {
                if (pack.level >= pack.levels.Length) continue;
                pack.level = pack.levels.Length;
                ApplyUpgrade(pack);
                if (pack.upgradeEffect == UpgradeEffect.ComboMeter && combo != null) combo.UserDisabled = true; // unlocked but OFF
            }
            else
            {
                if (pack.purchased) continue;
                pack.purchased = true;

                // Everything that has a switch in the Toggles window is unlocked but left switched OFF.
                if (pack.unlocksAutoClicker && autoClicker != null) autoClicker.UserDisabled = true;
                if (pack.unlocksGrabbing && grab != null) grab.UserDisabled = true;
                if (pack.unlocksTimeStop && timeStop != null) timeStop.UserDisabled = true;
                if (pack.unlocksRobot && robot != null) robot.UserDisabled = true;
                if (!string.IsNullOrEmpty(pack.unlocksMinigame))
                {
                    PixelMinigame game = PixelMinigame.Find(pack.unlocksMinigame);
                    if (game != null) game.UserDisabled = true;
                }
                ApplyPackEffects(pack);
            }
            onPackPurchased?.Invoke(i);
        }

        // Pixel types with a spawn switch are left switched off too.
        for (int t = 0; t < clicker.Tiers.Length; t++) clicker.SetSpawnEnabled(t, false);

        // So is every minigame / vendor that has no pack of its own (Stardust, the visitors).
        foreach (PixelMinigame game in PixelMinigame.All)
            if (game != null) game.UserDisabled = true;
        RefreshRows();   // (skills are left alone: the Dev Tools 'Max XP' widget does those)
    }

    // ---- Dev Tools 'Unlock' widget: one pack, or 'All shop items' = DevUnlockAll (every pack + every pixel) ----

    /// <summary>
    /// Gives the (one-off) pack that unlocks this pixel type for free, as if bought - Cubie's second visit hands over the RGB Pack this way, so
    /// everything that needs it keeps working. False if there is no such pack or it is already owned.
    /// </summary>
    public bool GrantPackRewarding(PixelClicker.PixelType type)
    {
        if (packs == null) return false;
        for (int i = 0; i < packs.Length; i++)
        {
            ShopPack p = packs[i];
            if (p == null || IsLeveled(p) || p.purchased || p.rewardTiers == null) continue;
            if (Array.FindIndex(p.rewardTiers, t => t != null && t.type == type) < 0) continue;
            p.purchased = true;
            ApplyPackEffects(p);
            onPackPurchased?.Invoke(i);
            RefreshRows();
            return true;
        }
        return false;
    }

    /// <summary>Dev tools: unlocks one shop pack for free, as if bought (an upgrade goes to its top level). Ignores requirements, costs and hideInShop.</summary>
    public void DevUnlockPack(int index)
    {
        if (packs == null || index < 0 || index >= packs.Length || packs[index] == null) return;
        ShopPack pack = packs[index];
        if (IsLeveled(pack))
        {
            if (pack.level >= pack.levels.Length) return;
            pack.level = pack.levels.Length;
            ApplyUpgrade(pack);
        }
        else
        {
            pack.purchased = true;
            ApplyPackEffects(pack);
        }
        onPackPurchased?.Invoke(index);
        RefreshRows();
    }

    /// <summary>Dev tools: takes one shop pack back (an upgrade goes to level 0; its reward pixels are locked again and their features switched off).</summary>
    public void DevRelockPack(int index)
    {
        if (packs == null || index < 0 || index >= packs.Length || packs[index] == null) return;
        ShopPack pack = packs[index];
        if (IsLeveled(pack))
        {
            pack.level = 0;
            ApplyUpgrade(pack);
            pack.appliedLevel = 0;
        }
        else
        {
            SetPackState(index, false, 0);
            if (pack.unlocksAutoClicker && autoClicker != null) autoClicker.UserDisabled = true;
            if (pack.rewardTiers != null)
                foreach (PixelClicker.PixelTier reward in pack.rewardTiers)
                    if (reward != null) clicker.LockTier(clicker.IndexOf(reward.type));
        }
        RefreshRows();
    }

    /// <summary>Dev tools: unlocks every shop pack (shop only: no pixels outside packs, no skill XP).</summary>
    [ContextMenu("Dev: Unlock All Packs")]
    public void DevUnlockAllPacks() { for (int i = 0; i < packs.Length; i++) DevUnlockPack(i); }

    /// <summary>Dev tools: relocks every shop pack.</summary>
    public void DevRelockAllPacks() { for (int i = 0; i < packs.Length; i++) DevRelockPack(i); }

    /// <summary>Buys a pack: spends all costs, unlocks the reward tiers. Returns false if not possible.</summary>
    public bool TryBuy(int packIndex)
    {
        if (packIndex < 0 || packIndex >= packs.Length) return false;
        if (IsPurchased(packIndex) || !IsRequirementMet(packIndex) || !CanAfford(packIndex)) return false;

        ShopPack pack = packs[packIndex];

        PackCost[] costs = CurrentCosts(pack);
        if (costs != null)
            foreach (PackCost cost in costs)
                SpendCost(cost);

        if (IsLeveled(pack))
        {
            pack.level++;
            AnnouncePurchase(pack, pack.displayName + " upgraded to level " + pack.level + "!");
            ApplyUpgrade(pack);
            switch (pack.upgradeEffect)
            {
                case UpgradeEffect.AutoClickerInterval: PixelHints.Trigger("upgrade_interval"); break;
                case UpgradeEffect.AutoClickerClicks: PixelHints.Trigger("upgrade_clicks"); break;
                case UpgradeEffect.ComboMeter: PixelHints.Trigger("upgrade_combo"); break;
                case UpgradeEffect.BankCapacity: PixelHints.Trigger("upgrade_bank"); break;
            }
        }
        else
        {
            pack.purchased = true;
            AnnouncePurchase(pack, "You bought " + pack.displayName + "!");
            ApplyPackEffects(pack);
            if (pack.unlocksRobot && robot != null) robot.UserDisabled = true;   // the UFO starts switched OFF (Toggles window)

            if (pack.unlocksCrafting) PixelHints.Trigger("crafting");
            if (pack.unlocksGrabbing) PixelHints.Trigger("grab");
            if (pack.unlocksTimeStop) PixelHints.Trigger("timestop");
            if (pack.unlocksTimeSlow) PixelHints.Trigger("timeslow");
            if (pack.unlocksBank) PixelHints.Trigger("bank");
            if (pack.unlocksRobot) PixelHints.Trigger("robot");
            if (pack.unlocksBlockHold) PixelHints.Trigger("blockhold");
            if (pack.rewardTiers != null)
                foreach (PixelClicker.PixelTier reward in pack.rewardTiers)
                {
                    if (PixelClicker.IsOre(reward.type) && reward.type != PixelClicker.PixelType.Copper) continue; // one tip for the whole Prospector's Pack
                    if (PixelClicker.IsFarm(reward.type) && reward.type != PixelClicker.PixelType.Dirt) continue;   // and one for the Farmer's Pack
                    if (PixelClicker.IsWoodBlock(reward.type)) continue;   // no Got-it box for the Lumberjack's Pack (the Skills window introduces Woodcutting)
                    if (PixelClicker.IsFishBlock(reward.type) && reward.type != PixelClicker.PixelType.Fish) continue;   // and one for the Angler's Pack
                    if (reward.type == PixelClicker.PixelType.Obsidian && Rewards(pack, PixelClicker.PixelType.Copper)) continue;   // Obsidian comes with the Prospector's Pack
                    if (PixelClicker.IsForageBlock(reward.type) && reward.type != PixelClicker.PixelType.Berry) continue;   // and one for the Forager's Pack
                    if (PixelClicker.IsHazard(reward.type)) continue;   // the hazard blocks (Pest, Gas Pocket, Hornet Nest) have no tip of their own
                    if (reward.type == PixelClicker.PixelType.Glass)
                    {
                        // Glass is switched on by default: a box beside the Toggles tab says where to switch it off.
                        PixelToggles.ShowHint(glassToggleHint);
                        PixelHints.Announce(glassUnlockedText, glassToggleHint);
                        continue;
                    }
                    PixelHints.Trigger("pixel_" + reward.type);
                }

            // The auto clicker starts switched off: a box points the player to the Toggles window to turn it on.
            if (pack.unlocksAutoClicker && autoClicker != null)
            {
                autoClicker.UserDisabled = true;
                if (PixelTogglesTutorial.WillRun) PixelTogglesTutorial.Request();   // Cubie explains the Toggles window (first-time tips)
                else PixelToggles.ShowHint();
                PixelHints.Announce(autoClickerUnlockedText, PixelToggles.HintMessage);
            }
        }

        PlayPurchaseSound();

        onPackPurchased?.Invoke(packIndex);
        RefreshRows();
        return true;
    }

    /// <summary>The bottom-left event line for a purchase; clicking it (or Enter) shows the pack's name and description.</summary>
    private void AnnouncePurchase(ShopPack pack, string line)
    {
        PixelStats.Count("shop.purchases");
        string info = "<b>" + pack.displayName + "</b>\n" + ResolveDescription(pack);
        PixelHints.Announce(line, info);
    }

    private void PlayPurchaseSound()
    {
        if (purchaseSound == null) return;
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
        audioSource.PlayOneShot(purchaseSound, soundVolume);
    }
}
