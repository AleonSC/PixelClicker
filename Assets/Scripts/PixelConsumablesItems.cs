// ---- System notes ----
// PixelConsumablesItems.cs
//   Part of PixelConsumables (partial class): the item list (potions then devices: names, owned, prices, categories, use) and the Operation upgrades. The system notes are in PixelConsumables.cs.
// ---------------------------------------------------
using System;
using TMPro;
using UnityEngine;

public partial class PixelConsumables
{
    // ------------------------------------------------------------------
    // Items: potions first, then devices (one list for the shop and the inventory)
    // ------------------------------------------------------------------

    /// <summary>Potions + devices.</summary>
    public int ItemCount => potions.Length + devices.Length;

    public bool IsDevice(int item) => item >= potions.Length;

    public string ItemName(int item) => IsDevice(item) ? devices[item - potions.Length].displayName : potions[item].displayName;

    /// <summary>True while the dev tools' "Infinite resources" is on: every consumable counts as plentiful and using one costs nothing.</summary>
    private static bool Inf => PixelClicker.InfiniteResources;

    /// <summary>How an owned count is shown: an infinity sign under Infinite resources (the dev tools' 99999), else the number.</summary>
    public static string OwnedText(int owned) => PixelClicker.InfiniteResources && owned >= 99999 ? "\u221E" : owned.ToString();

    public int ItemOwned(int item) => Inf ? 99999 : IsDevice(item) ? devices[item - potions.Length].owned : potions[item].owned;

    private PixelAutoClicker autoClicker;

    /// <summary>Clicks per second used for potion prices: the auto clicker's rate (once bought) plus the assumed manual rate.</summary>
    public double ExpectedClicksPerSecond()
    {
        if (autoClicker == null) autoClicker = PixelFind.First<PixelAutoClicker>();
        // Running = bought (even if switched off in Toggles), so switching it off can't make potions cheaper.
        double auto = autoClicker != null && autoClicker.Running && autoClicker.Interval > 0f
            ? autoClicker.ClicksPerTick / (double)autoClicker.Interval
            : 0d;
        return auto + assumedManualClicksPerSecond;
    }

    /// <summary>
    /// What a potion of this pixel type is expected to make over 'seconds': clicks x payout per click (Value, Ultra and
    /// multi-click included; tough pixels pay once per several clicks). A Vacuum potion also counts the old pixels its
    /// every-Nth-click vacuum re-collects.
    /// </summary>
    public double ExpectedPotionOutput(PixelClicker.PixelType type, float seconds)
    {
        if (clicker == null) return 0d;
        int t = clicker.IndexOf(type);
        if (t < 0) return 0d;

        PixelClicker.PixelTier tier = clicker.Tiers[t];
        double cps = ExpectedClicksPerSecond();
        double perClick = tier.amountPerClick * clicker.PayoutMultiplier(t) * clicker.ClickMultiplier / System.Math.Max(1, tier.clicksToCollect);
        double output = seconds * cps * perClick;
        if (tier.vacuum)
        {
            double onFloor = System.Math.Min(clicker.OldPixelCap, cps * clicker.FallingCopyLifetime);
            output *= 1d + onFloor / clicker.PotionVacuumEvery;
        }
        return output;
    }

    /// <summary>Price (in its own pixel) of a potion of this type lasting 'seconds', from its expected output.</summary>
    public double PotionPriceFromOutput(PixelClicker.PixelType type, float seconds) =>
        System.Math.Max(potionMinPrice, System.Math.Ceiling(ExpectedPotionOutput(type, seconds) / System.Math.Max(1f, potionTargetReturn)));

    /// <summary>Current shop price of the (buyable) potion of a pixel type, in that pixel. Used by Crafting.</summary>
    public double PotionShopPrice(PixelClicker.PixelType type)
    {
        for (int i = 0; i < potions.Length; i++)
        {
            Potion p = potions[i];
            if (p == null || p.craftOnly || p.type != type) continue;
            PixelShop.PackCost[] costs = ItemCosts(i);
            if (costs != null)
                foreach (PixelShop.PackCost c in costs)
                    if (c != null && string.IsNullOrEmpty(c.minigameCurrency) && c.type == type) return c.amount;
            break;
        }
        return PotionPriceFromOutput(type, 30f);
    }

    /// <summary>
    /// What one of an item costs right now. Potions: from their expected output (see <see cref="potionPriceFromOutput"/>).
    /// Devices (and potions with that off): the typed prices, scaled by each pixel's Value multiplier when that is on.
    /// </summary>
    /// <summary>A device with a rising price (Device.priceGrowthPerOwned): its costs times growth ^ (how many are held + standing + 'extra' already in this purchase).</summary>
    private PixelShop.PackCost[] Laddered(int item, PixelShop.PackCost[] costs, int extra = 0)
    {
        if (!IsDevice(item) || costs == null) return costs;
        int index = item - potions.Length;
        Device d = devices[index];
        if (d == null || d.priceGrowthPerOwned <= 1f) return costs;
        int have = d.owned;
        foreach (PixelPlacedDevice p in PixelPlacedDevice.All) if (p != null && !p.IsEnding && p.DeviceIndex == index) have++;
        have += Mathf.Max(0, extra);
        if (have <= 0) return costs;
        double factor = System.Math.Pow(d.priceGrowthPerOwned, have);
        PixelShop.PackCost[] up = new PixelShop.PackCost[costs.Length];
        for (int i = 0; i < costs.Length; i++)
        {
            PixelShop.PackCost c = costs[i];
            up[i] = c == null ? null : new PixelShop.PackCost { type = c.type, amount = System.Math.Ceiling(c.amount * factor), minigameCurrency = c.minigameCurrency };
        }
        return up;
    }

    public PixelShop.PackCost[] ItemCosts(int item) => ItemCosts(item, 0);

    /// <summary>
    /// What 'count' of an item cost together. A device with a rising price (the Recycler) adds up each step (75 + 150 + 300...),
    /// not 'count' times the price of the next one.
    /// </summary>
    public PixelShop.PackCost[] ItemCostsTotal(int item, int count)
    {
        if (count <= 0) return new PixelShop.PackCost[0];
        System.Collections.Generic.List<PixelShop.PackCost> total = new System.Collections.Generic.List<PixelShop.PackCost>();
        bool laddered = IsDevice(item) && devices[item - potions.Length] != null && devices[item - potions.Length].priceGrowthPerOwned > 1f;
        for (int k = 0; k < (laddered ? count : 1); k++)
        {
            PixelShop.PackCost[] step = ItemCosts(item, k);
            if (step == null) continue;
            int times = laddered ? 1 : count;
            foreach (PixelShop.PackCost c in step)
            {
                if (c == null) continue;
                PixelShop.PackCost same = total.Find(t => t.type == c.type && t.minigameCurrency == c.minigameCurrency);
                if (same != null) same.amount += c.amount * times;
                else total.Add(new PixelShop.PackCost { type = c.type, amount = c.amount * times, minigameCurrency = c.minigameCurrency });
            }
        }
        return total.ToArray();
    }

    private PixelShop.PackCost[] ItemCosts(int item, int extra)
    {
        if (IsDevice(item) && devices[item - potions.Length].kind == DeviceKind.Seed && clicker != null && !IsDragonSeed(item))
        {
            int t = clicker.IndexOf(devices[item - potions.Length].seedType);
            if (t < 0) return new PixelShop.PackCost[0];
            PixelClicker.PixelTier tier = clicker.Tiers[t];
            double rarity = seedRarityPricePower > 0f ? System.Math.Pow(1d / System.Math.Max(0.02d, tier.spawnWeight), seedRarityPricePower) : 1d;
            rarity = System.Math.Max(1d, rarity);
            double price = tier.amountPerClick * seedPriceFactor * rarity * (scalePricesWithValue ? clicker.ValueMultiplier(t) : 1d);
            return new[] { new PixelShop.PackCost { type = tier.type, amount = System.Math.Max(seedMinPrice, System.Math.Ceiling(price)) } };
        }

        if (!IsDevice(item) && potionPriceFromOutput && clicker != null && !potions[item].craftOnly)
        {
            Potion p = potions[item];
            return new[] { new PixelShop.PackCost { type = p.type, amount = PotionPriceFromOutput(p.type, p.durationSeconds) } };
        }

        PixelShop.PackCost[] baseCosts = IsDevice(item) ? devices[item - potions.Length].costs : potions[item].costs;
        baseCosts = Laddered(item, baseCosts, extra);
        if (!scalePricesWithValue || clicker == null || baseCosts == null) return baseCosts;

        PixelShop.PackCost[] scaled = new PixelShop.PackCost[baseCosts.Length];
        for (int i = 0; i < baseCosts.Length; i++)
        {
            PixelShop.PackCost c = baseCosts[i];
            if (c == null) continue;
            double factor = string.IsNullOrEmpty(c.minigameCurrency) ? clicker.ValueMultiplier(clicker.IndexOf(c.type)) : 1d;
            scaled[i] = new PixelShop.PackCost { type = c.type, amount = System.Math.Ceiling(c.amount * factor), minigameCurrency = c.minigameCurrency };
        }
        return scaled;
    }

    /// <summary>The pixel type that must be unlocked before the item is listed in the shop.</summary>
    public PixelClicker.PixelType ItemRequiredType(int item) =>
        IsDevice(item) ? devices[item - potions.Length].requiredType : potions[item].type;

    /// <summary>How long a placed device lasts: the Vacuum Device uses <see cref="vacuumDeviceSeconds"/> (default 5), the others their own setting.</summary>
    private float DeviceSeconds(Device d) => d.kind == DeviceKind.Vacuum ? (vacuumDeviceSeconds > 0f ? vacuumDeviceSeconds : 5f) + OperationBonus(DeviceKind.Vacuum, PixelOperationStat.Duration) : d.durationSeconds;

    // ------------------------------------------------------------------
    // Operation upgrades (bought in the shop's Upgrades > Operation sub-tab)
    // ------------------------------------------------------------------

    private PixelShop opShop;

    /// <summary>How much the bought Operation upgrades add to a stat of a consumable (0 when there are none).</summary>
    public float OperationBonus(DeviceKind kind, PixelOperationStat stat)
    {
        if (opShop == null) opShop = PixelFind.First<PixelShop>();
        return opShop != null ? opShop.OperationBonus(kind, stat) : 0f;
    }

    /// <summary>The 'radius' of the first device entry of this kind with its Operation upgrades applied (the Recycler's size factor), or -1 if there is none.</summary>
    public float EffectiveRadiusOf(DeviceKind kind)
    {
        if (devices != null) foreach (Device d in devices) if (d != null && d.kind == kind) return EffectiveDevice(d).radius;
        return -1f;
    }

    /// <summary>The device entry at this index with its Operation upgrades applied (null for a bad index). The device info panel reads run time / reach from it.</summary>
    public Device EffectiveDeviceAt(int index) => devices != null && index >= 0 && index < devices.Length && devices[index] != null ? EffectiveDevice(devices[index]) : null;

    /// <summary>A copy of the device with the Operation upgrades applied (duration, reach, strength, uses...). Used whenever a device is placed, previewed or described.</summary>
    private Device EffectiveDevice(Device d)
    {
        Device e = d.Clone();
        float B(PixelOperationStat st) => OperationBonus(d.kind, st);
        switch (d.kind)
        {
            case DeviceKind.Vacuum:
                e.durationSeconds = (vacuumDeviceSeconds > 0f ? vacuumDeviceSeconds : 5f) + B(PixelOperationStat.Duration);
                e.radius += B(PixelOperationStat.Radius);
                e.pullAcceleration += B(PixelOperationStat.Pull);
                break;
            case DeviceKind.Fan:
                e.durationSeconds += B(PixelOperationStat.Duration);
                e.radius += B(PixelOperationStat.Radius);
                e.blowAcceleration += B(PixelOperationStat.Blow);
                break;
            case DeviceKind.ChargeBooster:
                e.uses += Mathf.RoundToInt(B(PixelOperationStat.Uses));
                e.chargeCapacity = Mathf.Max(2f, e.chargeCapacity + B(PixelOperationStat.ChargeNeeded));
                e.superChargeSeconds += B(PixelOperationStat.SuperSeconds);
                break;
            case DeviceKind.Recycler:
                e.durationSeconds += B(PixelOperationStat.Duration);
                e.radius = Mathf.Max(0.5f, e.radius + B(PixelOperationStat.Radius));   // (the bin's size factor)
                break;
            case DeviceKind.LightningRod:
                e.uses += Mathf.RoundToInt(B(PixelOperationStat.Uses));
                e.strikeSeconds = Mathf.Max(0.6f, e.strikeSeconds + B(PixelOperationStat.StrikeInterval));
                e.radius += B(PixelOperationStat.Radius);
                break;
        }
        return e;
    }

    /// <summary>The first device of a kind (null if there is none).</summary>
    public Device FindDeviceOfKind(DeviceKind kind)
    {
        foreach (Device x in devices) if (x != null && !x.attuned && x.kind == kind) return x;
        return null;
    }

    /// <summary>The value of a stat before any upgrade (the number the upgrade tooltips start from).</summary>
    public float OperationBase(DeviceKind kind, PixelOperationStat stat)
    {
        Device d = FindDeviceOfKind(kind);
        if (d == null) return 0f;
        switch (stat)
        {
            case PixelOperationStat.Duration:
                if (kind == DeviceKind.Vacuum) return vacuumDeviceSeconds > 0f ? vacuumDeviceSeconds : 5f;
                if (kind == DeviceKind.PetTreat) return PixelPets.Instance != null ? PixelPets.Instance.TreatSeconds : 60f;
                return d.durationSeconds;
            case PixelOperationStat.Radius: return d.radius;
            case PixelOperationStat.Pull: return d.pullAcceleration;
            case PixelOperationStat.Blow: return d.blowAcceleration;
            case PixelOperationStat.Uses: return d.uses;
            case PixelOperationStat.ChargeNeeded: return d.chargeCapacity;
            case PixelOperationStat.SuperSeconds: return d.superChargeSeconds;
            case PixelOperationStat.StrikeInterval: return d.strikeSeconds;
            case PixelOperationStat.Storage: return d.maxHeld;
            case PixelOperationStat.RecyclerStreak: return PixelRecycler.DefaultStreakCap;
            case PixelOperationStat.RecyclerSuction: return PixelRecycler.DefaultSuctionRadius;
            case PixelOperationStat.Speed: return kind == DeviceKind.Recycler ? PixelRecycler.DefaultRecycleDelay : kind == DeviceKind.SortingHopper ? PixelSortingHopper.DefaultFeedInterval : 0f;
            case PixelOperationStat.Size: return kind == DeviceKind.SortingHopper ? 1f : 0f;
            case PixelOperationStat.Outputs: return kind == DeviceKind.SortingHopper ? 1f : 0f;   // (the front feed pipe; each level adds a side pipe)
            case PixelOperationStat.BaitGhosts:
            {
                PixelGhostMinigame ghosts = PixelFind.First<PixelGhostMinigame>();
                return ghosts != null ? ghosts.BaitGhostCount : 3f;
            }
        }
        return 0f;
    }

    /// <summary>The pixel type an upgrade of this consumable is paid in (the pixel the consumable itself needs).</summary>
    public PixelClicker.PixelType OperationCostType(DeviceKind kind)
    {
        Device d = FindDeviceOfKind(kind);
        return d != null ? d.requiredType : PixelClicker.PixelType.White;
    }

    /// <summary>Is a consumable of this kind available to show in the Operation tab (its pixel type is unlocked, or you hold one)?</summary>
    public bool OperationListed(DeviceKind kind)
    {
        Device d = FindDeviceOfKind(kind);
        return d != null && (clicker.IsUnlocked(d.requiredType) || d.owned > 0);
    }

    /// <summary>The name of the first consumable of a kind.</summary>
    public string OperationName(DeviceKind kind)
    {
        Device d = FindDeviceOfKind(kind);
        return d != null ? d.displayName : kind.ToString();
    }

    /// <summary>The description of the first consumable of a kind (with its upgrades applied).</summary>
    public string OperationDescription(DeviceKind kind)
    {
        Device d = FindDeviceOfKind(kind);
        if (d == null) return "";
        Device e = EffectiveDevice(d);
        return (e.description ?? "").Replace("{radius}", e.radius.ToString("0.##")).Replace("{duration}", DeviceSeconds(e).ToString("0.##"));
    }


    public string ItemDescription(int item)
    {
        if (!IsDevice(item)) return Describe(item);

        Device d = EffectiveDevice(devices[item - potions.Length]);
        string text = d.description ?? "";
        if (text.IndexOf('{') < 0) return text;
        int held = ItemCapacity(item);
        return text
            .Replace("{radius}", d.radius.ToString("0.##"))
            .Replace("{duration}", DeviceSeconds(d).ToString("0.##"))
            .Replace("{held}", held == int.MaxValue ? "any number" : held.ToString())
            .Replace("{charges}", PesticideSprayCharges.ToString())
            .Replace("{uses}", d.uses.ToString());
    }

    /// <summary>Gives the player devices of a kind (a gift, e.g. Cubie's Recycler). False if there is no such device.</summary>
    public bool GiveDevice(DeviceKind kind, int amount = 1)
    {
        if (devices == null || amount <= 0) return false;
        int index = Array.FindIndex(devices, d => d != null && d.kind == kind);
        if (index < 0) return false;
        AddItem(potions.Length + index, amount);
        return true;
    }

    /// <summary>Adds items to the inventory (a shop purchase).</summary>
    public void AddItem(int item, int amount = 1)
    {
        if (IsDevice(item))
        {
            if (amount > 0) devices[item - potions.Length].owned = ClampDeviceHeld(item - potions.Length, devices[item - potions.Length].owned + amount);
        }
        else
        {
            Add(item, amount);
        }
    }

    /// <summary>The kind of device an item index refers to (items past the potions are devices).</summary>
    /// <summary>The colour of a device item (used for crafting icons).</summary>
    public Color DeviceColorOf(int item) => devices[item - potions.Length].color;

    public DeviceKind DeviceKindOf(int item) => devices[item - potions.Length].kind;

    /// <summary>True for a seed item (a "device" of kind Seed: planted, not placed).</summary>
    /// <summary>Instant-use items (Combo Fuel, Ghost Bait, Pet Treat): they are not placed, so they live in the Supplies category.</summary>
    public bool IsSupplyItem(int item)
    {
        if (!IsDevice(item)) return false;
        DeviceKind k = devices[item - potions.Length].kind;
        return PixelInstantItems.Find(k) != null;   // (the list of instant-use items: PixelInstantItems)
    }

    /// <summary>0 = potion, 1 = placeable device (Utilities), 2 = seed, 3 = supply (instant-use item).</summary>
    public int CategoryOf(int item) => IsSeedItem(item) ? 2 : IsSupplyItem(item) && !UtilityTool(item) ? 3 : IsDevice(item) ? 1 : 0;

    /// <summary>An instant-use item listed with the Utilities (the Container Hose sits next to the machines it connects).</summary>
    private bool UtilityTool(int item) { PixelInstantItems.Entry e = PixelInstantItems.Find(devices[item - potions.Length].kind); return e != null && e.withUtilities; }

    /// <summary>How many items of this kind the player holds (huge with infinite resources).</summary>
    public int OwnedOfKind(DeviceKind kind)
    {
        if (Inf) return 99999;
        int n = 0;
        foreach (Device d in devices) if (d != null && d.kind == kind) n += d.owned;
        return n;
    }

    public bool IsSeedItem(int item) => IsDevice(item) && devices[item - potions.Length].kind == DeviceKind.Seed;

    /// <summary>True for the rare Dragon Seed (typed prices, offered only by the Farmer, grows a Dragon Cube the player lacks).</summary>
    public bool IsDragonSeed(int item) => IsSeedItem(item) && !devices[item - potions.Length].beans && PixelClicker.IsDragonCube(devices[item - potions.Length].seedType);

    /// <summary>The item index of the Magic Beans, or -1.</summary>
    public int BeansItem()
    {
        for (int i = 0; i < devices.Length; i++) if (devices[i] != null && devices[i].beans) return potions.Length + i;
        return -1;
    }

    /// <summary>The pixel type a seed item grows.</summary>
    public PixelClicker.PixelType SeedTypeOf(int item) => devices[item - potions.Length].seedType;

    /// <summary>Takes items out of the inventory (crafting). Returns false if you don't have that many.</summary>
    public bool TryRemoveItem(int item, int amount)
    {
        if (item < 0 || item >= ItemCount || amount <= 0 || ItemOwned(item) < amount) return false;
        if (Inf) return true;
        if (IsDevice(item)) devices[item - potions.Length].owned -= amount;
        else potions[item].owned -= amount;
        return true;
    }

    /// <summary>Right-click use: drinks a potion, or starts placing a device.</summary>
    public bool TryUseItem(int item)
    {
        if (!IsDevice(item)) return TryConsume(item);
        int index = item - potions.Length;
        // Instant-use items (one data-driven list: PixelInstantItems); everything else is placed, or planted (seeds).
        PixelInstantItems.Entry instant = PixelInstantItems.Find(devices[index].kind);
        if (instant == null) return BeginPlacement(index);
        bool ok = instant.use(this, item, out string message);
        if (ok)
        {
            if (instant.usedUp && !Inf) devices[index].owned--;
            if (!string.IsNullOrEmpty(instant.stat)) PixelStats.Count(instant.stat);
            if (!string.IsNullOrEmpty(instant.sound)) PixelAudio.Play(instant.sound);
        }
        if (!string.IsNullOrEmpty(message)) PixelHints.Announce(message);
        return ok;
    }

    /// <summary>Pesticide charges one Pesticide Spray adds (PixelInstantItems).</summary>
    public int PesticideSprayCharges => Mathf.Max(1, pesticideSprayCharges);

    /// <summary>
    /// Uses one held item of this kind without the player doing anything (Combo Fuel when the combo bar runs out).
    /// Returns false when none is held.
    /// </summary>
    public bool TryUseAutoItem(DeviceKind kind)
    {
        for (int i = 0; i < devices.Length; i++)
        {
            if (devices[i] == null || devices[i].kind != kind || (!Inf && devices[i].owned <= 0)) continue;
            if (!Inf) devices[i].owned--;
            return true;
        }
        return false;
    }

    /// <summary>The two Biomass Generators (one behaviour, PixelBiomassGenerator).</summary>
    public static bool IsGeneratorKind(DeviceKind kind) => kind == DeviceKind.PocketBiomassGenerator || kind == DeviceKind.BiomassGenerator;

    /// <summary>
    /// Places a working device of this kind at once, using nothing up (Dev Hub > Tester buttons for devices not on sale yet: the Junction Box).
    /// It is a normal placed device: saved with the placed devices, removed the usual way. Null if there is no such device entry.
    /// </summary>
    public GameObject PlaceDeviceNow(DeviceKind kind, Vector3 point, float yaw)
    {
        int index = Array.FindIndex(devices, x => x != null && x.kind == kind);
        if (index < 0) return null;
        Device effective = EffectiveDevice(devices[index]);
        PixelPlacedDevice placed = SpawnDevice(index, point, yaw, UsesKind(kind) ? effective.uses : DeviceSeconds(effective), 0f);
        if (placed == null) return null;
        if (StartsDisarmed(kind)) placed.Disarm();   // (like a device the player places)
        PixelAudio.Play("device_place");
        DevicePlaced?.Invoke(kind);
        onDevicePlaced?.Invoke(index);
        return placed.gameObject;
    }

    // Device access for the save system.
    public int DeviceCount => devices.Length;

    public Device GetDevice(int index) => devices[index];


    /// <summary>Description with {pixel} and {duration} filled in.</summary>
    public string Describe(int index)
    {
        Potion potion = potions[index];
        return (potion.description ?? "")
            .Replace("{pixel}", TierName(potion.type))
            .Replace("{pixel2}", TierName(potion.secondType))
            .Replace("{pixel3}", TierName(potion.thirdType))
            .Replace("{duration}", potion.durationSeconds.ToString("0.##"));
    }
}
