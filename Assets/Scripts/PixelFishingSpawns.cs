// ---- System notes ----
// PixelFishingSpawns.cs
//   Fishing's FIXED spawn table (part of partial class PixelClicker; used by PickSpawnTier and SpawnShare while Fishing is the skill in
//   play, `disableFishingTable` = the old filler / hazard weights): Water 80%, Driftwood 5%, Seaweed 7%, Jellyfish 4%, Shell 3%, Fish 1%
//   (`fishWaterShare` ... `fishFishShare`, 0 = these defaults; they are weights, so they are normalised if one block is missing). Everything
//   else comes from a replacement roll: a rolled Fish becomes a WHALE `whaleChance` (0 = 10%) of the time, a rolled Seaweed or Shell
//   becomes a message in a BOTTLE `bottleChance` (0 = 8%), a rolled Shell can instead become a PEARL `pearlChance` (0 = 6%), and
//   collecting a Water pixel makes the NEXT pixel a Bait block with `baitChance` (0 = 10%, QueueBaitAfter). Bait pellets still raise Fish's
//   weight (PixelBait.FishMultiplier) and active pets their type's (PixelPets.SpawnMultiplier). SpawnShare (the Pixel Guide) works out
//   the same numbers, bait included.
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public partial class PixelClicker
{
    [Header("Fishing spawn table")]
    [Tooltip("Use the old filler / hazard weights for Fishing instead of the fixed table.")]
    [SerializeField] private bool disableFishingTable = false;

    [Min(0f)] [Tooltip("Water's share of the rolls (it is a weight; 0 = 0.80).")]
    [SerializeField] private float fishWaterShare = 0f;
    [Min(0f)] [Tooltip("Driftwood's share (0 = 0.05).")]
    [SerializeField] private float fishDriftwoodShare = 0f;
    [Min(0f)] [Tooltip("Seaweed's share (0 = 0.07).")]
    [SerializeField] private float fishSeaweedShare = 0f;
    [Min(0f)] [Tooltip("Jellyfish's share (0 = 0.04).")]
    [SerializeField] private float fishJellyShare = 0f;
    [Min(0f)] [Tooltip("Shell's share (0 = 0.03).")]
    [SerializeField] private float fishShellShare = 0f;
    [Min(0f)] [Tooltip("Fish's share (0 = 0.01).")]
    [SerializeField] private float fishFishShare = 0f;
    [Range(0f, 1f)] [Tooltip("Chance a rolled Fish is a Whale instead (0 = 0.10).")]
    [SerializeField] private float whaleChance = 0f;
    [Range(0f, 1f)] [Tooltip("Chance a rolled Seaweed or Shell is a message in a Bottle instead (0 = 0.08).")]
    [SerializeField] private float bottleChance = 0f;
    [Range(0f, 1f)] [Tooltip("Chance a rolled Shell is a Pearl instead (0 = 0.06).")]
    [SerializeField] private float pearlChance = 0f;
    [Range(0f, 1f)] [Tooltip("Chance that collecting a Water pixel makes the next pixel a Bait block (0 = 0.10).")]
    [SerializeField] private float baitChance = 0f;

    private bool baitNext;

    private float WhaleP => whaleChance > 0f ? whaleChance : 0.10f;
    private float BottleP => bottleChance > 0f ? bottleChance : 0.08f;
    private float PearlP => pearlChance > 0f ? pearlChance : 0.06f;
    private float BaitP => baitChance > 0f ? baitChance : 0.10f;

    private bool FishingTableOn()
    {
        if (disableFishingTable) return false;
        PixelSkills s = PixelSkills.Instance;
        return s != null && s.ActiveId == PixelSkills.FishingId;
    }

    private bool FishAvailable(int i) => i >= 0 && tiers[i].CanSpawn && !PixelSkills.LeftOutOfSpawns(tiers[i].type);

    /// <summary>The six base blocks of the table with their weights (pets and bait pellets applied); false when Water is missing.</summary>
    private bool FishingBase(List<KeyValuePair<int, float>> list)
    {
        void Add(PixelType type, float share, float fallback, float mult = 1f)
        {
            int i = IndexOf(type);
            if (FishAvailable(i)) list.Add(new KeyValuePair<int, float>(i, (share > 0f ? share : fallback) * PixelPets.SpawnMultiplier(type) * mult));
        }
        Add(PixelType.Water, fishWaterShare, 0.80f);
        if (list.Count == 0) return false;
        Add(PixelType.Driftwood, fishDriftwoodShare, 0.05f);
        Add(PixelType.Seaweed, fishSeaweedShare, 0.07f);
        Add(PixelType.Jellyfish, fishJellyShare, 0.04f);
        Add(PixelType.Shell, fishShellShare, 0.03f);
        Add(PixelType.Fish, fishFishShare, 0.01f, PixelBait.FishMultiplier);
        return true;
    }

    /// <summary>Called when a pixel is collected: a Water pixel may make the next one Bait.</summary>
    private void QueueBaitAfter(PixelType collected)
    {
        if (collected != PixelType.Water || !FishingTableOn() || !FishAvailable(IndexOf(PixelType.Bait))) return;
        if (UnityEngine.Random.value < BaitP) baitNext = true;
    }

    /// <summary>Picks the next pixel with the fixed Fishing table. False (nothing picked) when Fishing's table is not in use.</summary>
    private bool TryPickFishing(out int result)
    {
        result = -1;
        if (!FishingTableOn()) { baitNext = false; return false; }
        int bait = IndexOf(PixelType.Bait);
        if (baitNext)
        {
            baitNext = false;
            if (FishAvailable(bait)) { result = bait; return true; }
        }
        List<KeyValuePair<int, float>> list = new List<KeyValuePair<int, float>>();
        if (!FishingBase(list)) return false;
        float total = 0f;
        foreach (KeyValuePair<int, float> kv in list) total += kv.Value;
        float roll = UnityEngine.Random.value * total;
        int pick = list[list.Count - 1].Key;
        foreach (KeyValuePair<int, float> kv in list)
        {
            roll -= kv.Value;
            if (roll <= 0f) { pick = kv.Key; break; }
        }

        PixelType type = tiers[pick].type;
        int whale = IndexOf(PixelType.Whale), bottle = IndexOf(PixelType.Bottle), pearl = IndexOf(PixelType.Pearl);
        if (type == PixelType.Fish && FishAvailable(whale) && UnityEngine.Random.value < WhaleP) pick = whale;
        else if ((type == PixelType.Seaweed || type == PixelType.Shell) && FishAvailable(bottle) && UnityEngine.Random.value < BottleP) pick = bottle;
        else if (type == PixelType.Shell && FishAvailable(pearl) && UnityEngine.Random.value < PearlP) pick = pearl;
        result = pick;
        return true;
    }

    /// <summary>The share (0..1) of the cube's pixels that are tier i under the Fishing table (bait included); false when the table is not in use.</summary>
    private bool TryFishingShare(int i, out float share)
    {
        share = 0f;
        if (!FishingTableOn()) return false;
        List<KeyValuePair<int, float>> list = new List<KeyValuePair<int, float>>();
        if (!FishingBase(list)) return false;
        float total = 0f;
        foreach (KeyValuePair<int, float> kv in list) total += kv.Value;
        PixelType type = tiers[i].type;
        float Base(PixelType t) { int k = IndexOf(t); foreach (KeyValuePair<int, float> kv in list) if (kv.Key == k) return kv.Value / total; return 0f; }
        bool whaleOn = FishAvailable(IndexOf(PixelType.Whale)), bottleOn = FishAvailable(IndexOf(PixelType.Bottle)), pearlOn = FishAvailable(IndexOf(PixelType.Pearl));
        bool baitOn = FishAvailable(IndexOf(PixelType.Bait));

        float fish = Base(PixelType.Fish), seaweed = Base(PixelType.Seaweed), shell = Base(PixelType.Shell), water = Base(PixelType.Water);
        float p = 0f;
        switch (type)
        {
            case PixelType.Water: case PixelType.Driftwood: case PixelType.Jellyfish: p = Base(type); break;
            case PixelType.Fish: p = fish * (whaleOn ? 1f - WhaleP : 1f); break;
            case PixelType.Whale: p = whaleOn ? fish * WhaleP : 0f; break;
            case PixelType.Seaweed: p = seaweed * (bottleOn ? 1f - BottleP : 1f); break;
            case PixelType.Shell: p = shell * (bottleOn ? 1f - BottleP : 1f) * (pearlOn ? 1f - PearlP : 1f); break;
            case PixelType.Bottle: p = bottleOn ? (seaweed + shell) * BottleP : 0f; break;
            case PixelType.Pearl: p = pearlOn ? shell * (bottleOn ? 1f - BottleP : 1f) * PearlP : 0f; break;
            case PixelType.Bait: break;
            default: return false;
        }
        if (baitOn)
        {
            // After a Water pixel the next one is Bait with baitChance: the stationary mix works out as below.
            float b = BaitP, w = water / (1f + b * water), baitShare = b * w;
            share = type == PixelType.Bait ? baitShare : p * (1f - baitShare);
        }
        else share = type == PixelType.Bait ? 0f : p;
        return true;
    }
}
