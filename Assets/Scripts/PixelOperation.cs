using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>What an Operation upgrade changes about a consumable.</summary>
public enum PixelOperationStat
{
    /// <summary>Seconds a placed device works (Vacuum, Fan, Sorter) or a Pet Treat lasts.</summary>
    Duration = 0,
    /// <summary>Reach in world units (Vacuum pull range, Fan length, Lightning Rod strike reach).</summary>
    Radius = 1,
    /// <summary>Vacuum pull strength (acceleration).</summary>
    Pull = 2,
    /// <summary>Fan wind strength (acceleration).</summary>
    Blow = 3,
    /// <summary>The top of the Sorter's force slider.</summary>
    ForceMax = 4,
    /// <summary>Uses a Charge Booster (clicks) or Lightning Rod (strikes) lasts.</summary>
    Uses = 5,
    /// <summary>Charge a Charge Booster needs before its super charge (lower is better).</summary>
    ChargeNeeded = 6,
    /// <summary>How long a Charge Booster's super charge lasts.</summary>
    SuperSeconds = 7,
    /// <summary>Seconds between a Lightning Rod's strikes (lower is better).</summary>
    StrikeInterval = 8,
    /// <summary>How many of the item you can hold (Combo Fuel, Ghost Bait, Pet Treat).</summary>
    Storage = 9,
    /// <summary>Extra ghosts a Ghost Bait calls.</summary>
    BaitGhosts = 10,
}

/// <summary>
/// One upgrade of a consumable, bought in the shop's Upgrades > Operation sub-tab. The level is kept as a stat counter
/// ("op.&lt;id&gt;", saved with the other stats). Costs are paid in the pixel type the consumable itself needs.
/// </summary>
[Serializable]
public class PixelOperationUpgrade
{
    [Tooltip("Unique id (the level is saved under it - don't change it once players have bought levels).")]
    public string id = "";

    [Tooltip("Name shown in the upgrade window.")]
    public string displayName = "Upgrade";

    [TextArea(1, 3)]
    [Tooltip("Extra line shown in the orange tooltip.")]
    public string description = "";

    [Tooltip("Which consumable this upgrades.")]
    public PixelConsumables.DeviceKind device = PixelConsumables.DeviceKind.Vacuum;

    [Tooltip("What it changes.")]
    public PixelOperationStat stat = PixelOperationStat.Duration;

    [Min(1)]
    [Tooltip("How many levels there are.")]
    public int maxLevel = 5;

    [Tooltip("How much each level adds to the stat (negative = lowers it).")]
    public float perLevel = 1f;

    [Min(0)]
    [Tooltip("Price of level 1, in the pixel type the consumable needs.")]
    public double baseCost = 30d;

    [Min(1f)]
    [Tooltip("Each level costs this many times the one before.")]
    public float costGrowth = 1.9f;

    [Tooltip("Shown after the numbers in the tooltip (s, x, ...).")]
    public string unit = "";

    /// <summary>The built-in upgrades.</summary>
    public static List<PixelOperationUpgrade> CreateDefaults()
    {
        PixelConsumables.DeviceKind V = PixelConsumables.DeviceKind.Vacuum, F = PixelConsumables.DeviceKind.Fan, S = PixelConsumables.DeviceKind.Sorter,
            B = PixelConsumables.DeviceKind.ChargeBooster, R = PixelConsumables.DeviceKind.LightningRod, C = PixelConsumables.DeviceKind.ComboFuel,
            G = PixelConsumables.DeviceKind.GhostBait, P = PixelConsumables.DeviceKind.PetTreat;
        PixelOperationUpgrade U(string id, string name, string desc, PixelConsumables.DeviceKind kind, PixelOperationStat stat, int levels, float per, double cost, string unit = "", float growth = 1.9f)
            => new PixelOperationUpgrade { id = id, displayName = name, description = desc, device = kind, stat = stat, maxLevel = levels, perLevel = per, baseCost = cost, unit = unit, costGrowth = growth };

        return new List<PixelOperationUpgrade>
        {
            U("vac_duration", "Run Time", "How long a placed Vacuum Device works.", V, PixelOperationStat.Duration, 6, 1.5f, 30, " s"),
            U("vac_radius", "Suction Range", "How far from the device old pixels are pulled in.", V, PixelOperationStat.Radius, 5, 0.5f, 40),
            U("vac_pull", "Pull Strength", "How hard old pixels are pulled towards the device.", V, PixelOperationStat.Pull, 5, 6f, 40),

            U("fan_duration", "Run Time", "How long a placed Fan blows.", F, PixelOperationStat.Duration, 6, 4f, 30, " s"),
            U("fan_radius", "Reach", "How long the blown cone is.", F, PixelOperationStat.Radius, 5, 0.8f, 40),
            U("fan_blow", "Wind Power", "How hard the Fan pushes old pixels.", F, PixelOperationStat.Blow, 5, 3f, 40),

            U("sort_duration", "Run Time", "How long a placed Sorter works.", S, PixelOperationStat.Duration, 6, 6f, 40, " s"),
            U("sort_force", "Max Force", "Raises the top of the force slider on the Sorter's pipe.", S, PixelOperationStat.ForceMax, 6, 0.5f, 40, "x"),

            U("boost_uses", "Click Capacity", "How many auto-clicker clicks a Charge Booster lasts.", B, PixelOperationStat.Uses, 6, 25f, 40),
            U("boost_charge", "Charge Needed", "Less Electric charge is needed before the super charge (lower is better).", B, PixelOperationStat.ChargeNeeded, 5, -1f, 50),
            U("boost_super", "Super Charge Length", "How long the super charge lasts.", B, PixelOperationStat.SuperSeconds, 5, 1.5f, 50, " s"),

            U("rod_uses", "Strikes", "How many lightning strikes a Lightning Rod lasts.", R, PixelOperationStat.Uses, 6, 3f, 40),
            U("rod_rate", "Strike Rate", "Seconds between strikes (lower is better).", R, PixelOperationStat.StrikeInterval, 5, -0.4f, 50, " s"),
            U("rod_radius", "Strike Reach", "How far from the rod old pixels are struck.", R, PixelOperationStat.Radius, 5, 0.6f, 40),

            U("fuel_storage", "Storage", "How many Combo Fuels you can hold.", C, PixelOperationStat.Storage, 5, 1f, 30),

            U("bait_storage", "Storage", "How many Ghost Baits you can hold.", G, PixelOperationStat.Storage, 3, 1f, 40),
            U("bait_ghosts", "Extra Ghosts", "How many more ghosts a Ghost Bait calls.", G, PixelOperationStat.BaitGhosts, 4, 1f, 60),

            U("treat_storage", "Storage", "How many Pet Treats you can hold.", P, PixelOperationStat.Storage, 5, 2f, 30),
            U("treat_duration", "Treat Length", "How long a Pet Treat keeps your pets hyper.", P, PixelOperationStat.Duration, 5, 15f, 40, " s"),
        };
    }
}
