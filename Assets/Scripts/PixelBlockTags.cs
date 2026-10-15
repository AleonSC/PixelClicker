// ---- System notes ----
// PixelBlockTags.cs
//   ONE PLACE that says what kind of block each pixel type is (step 1 of 'one definition per block'). Each block has a set of TAGS (PixelTag flags: Wood, Forage, Fish,
//   Farm, Hazard, Ore, RetiredOre, Standalone, DragonCube, RareCollectible, PrimeCube, Plant, Electrical) in the table below, and the old helper questions on PixelClicker
//   (IsWoodBlock, IsHazard, IsStandalone, IsOre, IsFarm, IsForageBlock, IsFishBlock, IsRetiredOre, IsDragonCube, IsGodCube, IsInfinityStone, IsPlant, IsElectricalBlock) now just
//   read it. ADDING A BLOCK: add one line here with its tags (e.g. a new Woodcutting block = PixelTag.Wood | PixelTag.Standalone) instead of editing several hand-written lists.
//   The table was generated from the old helpers and checked to give exactly the same answers for every pixel type. A type that is not listed has no tags.
// ---------------------------------------------------
using PixelType = PixelClicker.PixelType;

/// <summary>What kind of block a pixel type is (flags: a block can be several things at once, e.g. Wood and Standalone).</summary>
[System.Flags]
public enum PixelTag
{
    None = 0,
    Wood = 1 << 0,            // the Woodcutting skill's blocks (IsWoodBlock)
    Forage = 1 << 1,          // the Foraging skill's blocks (IsForageBlock)
    Fish = 1 << 2,            // the Fishing skill's blocks (IsFishBlock)
    Farm = 1 << 3,            // the Farming skill's blocks (IsFarm)
    Hazard = 1 << 4,          // negative blocks: hit them and they go off (IsHazard)
    Ore = 1 << 5,             // the Prospector / Mining ores (IsOre)
    RetiredOre = 1 << 6,      // ores that no longer spawn (IsRetiredOre)
    Standalone = 1 << 7,      // no potions / seeds / crafting / pets / achievements / Value upgrades (IsStandalone)
    DragonCube = 1 << 8,      // the seven Dragon Cubes (IsDragonCube)
    RareCollectible = 1 << 9, // God Cubes, Prime Cubes and the Cubelette: one of each, own find shows (IsGodCube)
    PrimeCube = 1 << 10,      // the six Prime Cubes (IsInfinityStone)
    Plant = 1 << 11,          // what cracked Seed shells grow (IsPlant)
    Electrical = 1 << 12,     // the Electrician pack's own blocks (IsElectricalBlock)
}

public static class PixelBlockTags
{
    private struct Entry { public PixelType type; public PixelTag tags; }
    private static Entry T(PixelType type, PixelTag tags) => new Entry { type = type, tags = tags };

    // ---- THE TABLE: one line per block that has any tags ----
    private static readonly Entry[] Table =
    {
        T(PixelType.DragonCube1, PixelTag.DragonCube),
        T(PixelType.DragonCube2, PixelTag.DragonCube),
        T(PixelType.DragonCube3, PixelTag.DragonCube),
        T(PixelType.DragonCube4, PixelTag.DragonCube),
        T(PixelType.DragonCube5, PixelTag.DragonCube),
        T(PixelType.DragonCube6, PixelTag.DragonCube),
        T(PixelType.DragonCube7, PixelTag.DragonCube),
        T(PixelType.Seed, PixelTag.Farm),
        T(PixelType.Water, PixelTag.Farm),
        T(PixelType.Fire, PixelTag.Standalone),
        T(PixelType.Copper, PixelTag.Ore | PixelTag.Standalone),
        T(PixelType.Tin, PixelTag.Ore | PixelTag.Standalone),
        T(PixelType.Iron, PixelTag.Ore | PixelTag.Standalone),
        T(PixelType.Lead, PixelTag.Ore | PixelTag.RetiredOre | PixelTag.Standalone),
        T(PixelType.Zinc, PixelTag.Ore | PixelTag.RetiredOre | PixelTag.Standalone),
        T(PixelType.Nickel, PixelTag.Ore | PixelTag.RetiredOre | PixelTag.Standalone),
        T(PixelType.Treasure, PixelTag.Standalone),
        T(PixelType.Nuclear, PixelTag.Standalone),
        T(PixelType.Wood, PixelTag.Wood | PixelTag.Standalone),
        T(PixelType.Rock, PixelTag.Standalone),
        T(PixelType.Dirt, PixelTag.Farm | PixelTag.Standalone),
        T(PixelType.Fertilizer, PixelTag.Farm | PixelTag.Standalone),
        T(PixelType.Pollen, PixelTag.Farm | PixelTag.Standalone | PixelTag.Plant),
        T(PixelType.Wheat, PixelTag.Farm | PixelTag.Standalone | PixelTag.Plant),
        T(PixelType.Weevil, PixelTag.Farm | PixelTag.Standalone),
        T(PixelType.GoldenFruit, PixelTag.Farm | PixelTag.Standalone | PixelTag.Plant),
        T(PixelType.Wire, PixelTag.Standalone | PixelTag.Electrical),
        T(PixelType.Battery, PixelTag.Standalone | PixelTag.Electrical),
        T(PixelType.Capacitor, PixelTag.Standalone | PixelTag.Electrical),
        T(PixelType.Resistor, PixelTag.Standalone | PixelTag.Electrical),
        T(PixelType.Transistor, PixelTag.Standalone | PixelTag.Electrical),
        T(PixelType.Fuse, PixelTag.Standalone | PixelTag.Electrical),
        T(PixelType.Bulb, PixelTag.Standalone | PixelTag.Electrical),
        T(PixelType.Pest, PixelTag.Hazard | PixelTag.Standalone),
        T(PixelType.GasPocket, PixelTag.Hazard | PixelTag.Standalone),
        T(PixelType.Bark, PixelTag.Wood | PixelTag.Standalone),
        T(PixelType.Sap, PixelTag.Wood | PixelTag.Standalone),
        T(PixelType.Leaf, PixelTag.Wood | PixelTag.Standalone),
        T(PixelType.Heartwood, PixelTag.Wood | PixelTag.Standalone),
        T(PixelType.Twig, PixelTag.Wood | PixelTag.Standalone),
        T(PixelType.HornetNest, PixelTag.Hazard | PixelTag.Standalone),
        T(PixelType.Berry, PixelTag.Forage | PixelTag.Standalone),
        T(PixelType.Mushroom, PixelTag.Forage | PixelTag.Standalone),
        T(PixelType.Herb, PixelTag.Forage | PixelTag.Standalone),
        T(PixelType.Truffle, PixelTag.Forage | PixelTag.Standalone),
        T(PixelType.Weeds, PixelTag.Forage | PixelTag.Standalone),
        T(PixelType.Toadstool, PixelTag.Hazard | PixelTag.Standalone),
        T(PixelType.Silver, PixelTag.Ore | PixelTag.Standalone),
        T(PixelType.Gold, PixelTag.Ore | PixelTag.Standalone),
        T(PixelType.Bedrock, PixelTag.Ore | PixelTag.Standalone),
        T(PixelType.Fish, PixelTag.Fish | PixelTag.Standalone),
        T(PixelType.Seaweed, PixelTag.Fish | PixelTag.Standalone),
        T(PixelType.Shell, PixelTag.Fish | PixelTag.Standalone),
        T(PixelType.Pearl, PixelTag.Fish | PixelTag.Standalone),
        T(PixelType.Driftwood, PixelTag.Fish | PixelTag.Standalone),
        T(PixelType.Jellyfish, PixelTag.Hazard | PixelTag.Standalone),
        T(PixelType.Bait, PixelTag.Fish | PixelTag.Standalone),
        T(PixelType.Bottle, PixelTag.Fish | PixelTag.Standalone),
        T(PixelType.Whale, PixelTag.Fish | PixelTag.Standalone),
        T(PixelType.Mold, PixelTag.Hazard | PixelTag.Standalone),
        T(PixelType.WoodChips, PixelTag.Standalone),
        T(PixelType.GreatOak, PixelTag.Wood | PixelTag.Standalone),
        T(PixelType.Whetstone, PixelTag.Standalone),
        T(PixelType.Acorn, PixelTag.Standalone),
        T(PixelType.Pesticide, PixelTag.Standalone),
        T(PixelType.Charcoal, PixelTag.Standalone),
        T(PixelType.GodSlifer, PixelTag.RareCollectible),
        T(PixelType.GodRa, PixelTag.RareCollectible),
        T(PixelType.GodObelisk, PixelTag.RareCollectible),
        T(PixelType.StoneSpace, PixelTag.RareCollectible | PixelTag.PrimeCube),
        T(PixelType.StoneMind, PixelTag.RareCollectible | PixelTag.PrimeCube),
        T(PixelType.StoneReality, PixelTag.RareCollectible | PixelTag.PrimeCube),
        T(PixelType.StonePower, PixelTag.RareCollectible | PixelTag.PrimeCube),
        T(PixelType.StoneTime, PixelTag.RareCollectible | PixelTag.PrimeCube),
        T(PixelType.StoneSoul, PixelTag.RareCollectible | PixelTag.PrimeCube),
        T(PixelType.InfinityCubelette, PixelTag.RareCollectible),
        T(PixelType.Pinecone, PixelTag.Wood | PixelTag.Standalone),
        T(PixelType.Moss, PixelTag.Wood | PixelTag.Standalone),
    };

    private static PixelTag[] byType;

    private static void Build()
    {
        int max = 0;
        foreach (Entry e in Table) if ((int)e.type > max) max = (int)e.type;
        PixelTag[] arr = new PixelTag[max + 1];
        foreach (Entry e in Table) arr[(int)e.type] |= e.tags;
        byType = arr;
    }

    /// <summary>All the tags of a pixel type (None for an unlisted type).</summary>
    public static PixelTag Of(PixelType type)
    {
        if (byType == null) Build();
        int i = (int)type;
        return i >= 0 && i < byType.Length ? byType[i] : PixelTag.None;
    }

    /// <summary>Does this pixel type have this tag (or any of these tags, when several are combined)?</summary>
    public static bool Has(PixelType type, PixelTag tag) => (Of(type) & tag) != 0;
}
