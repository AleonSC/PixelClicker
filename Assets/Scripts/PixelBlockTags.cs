// ---- System notes ----
// PixelBlockTags.cs
//   The PixelTag flags (what KIND of block a pixel type is) and PixelBlockTags.Has / Of. The tags themselves are set on each block's card in PixelBlockCatalog.cs
//   (this file is kept so a copy over an older Assets/Scripts folder does not leave a duplicate behind).
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
    Standalone = 1 << 7,      // a skill / special block: no seeds, not in 3-type combo potions, not in visitor gifts / trades (it HAS potions, 2-type combos, pets, achievements and Value upgrades) (IsStandalone)
    DragonCube = 1 << 8,      // the seven Dragon Cubes (IsDragonCube)
    RareCollectible = 1 << 9, // God Cubes, Prime Cubes and the Cubelette: one of each, own find shows (IsGodCube)
    PrimeCube = 1 << 10,      // the six Prime Cubes (IsInfinityStone)
    Plant = 1 << 11,          // what cracked Seed shells grow (IsPlant)
    Electrical = 1 << 12,     // the Electrician pack's own blocks (IsElectricalBlock)
}

/// <summary>Tag questions (kept as their own small class so callers read simply): PixelBlockTags.Has(type, PixelTag.Wood).</summary>
public static class PixelBlockTags
{
    public static PixelTag Of(PixelType type) => PixelBlockCatalog.TagsOf(type);
    public static bool Has(PixelType type, PixelTag tag) => (Of(type) & tag) != 0;
}
