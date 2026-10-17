// ---- System notes ----
// PixelClickerTiers.cs
//   Part of partial class PixelClicker: the tier list's helpers. Setting up the rare tiers at start (EnsureDragonCubes, EnsureGodCubes - God
//   and Prime Cubes and the Cubelette -, EnsureGoldenSnitch, with goldenSnitchSpawnWeight), and the "Tier helpers" section: which tier is on
//   the cube (GetClickTierIndex), spawn picking, unlocks, counts, the Block Hold hooks. Split out of PixelClicker.cs (no behaviour change).
// ---------------------------------------------------
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
    /// <summary>The seven Dragon Cube tiers: orange, 1 payout each, an extremely low spawn weight and no requirements (rare drops).</summary>
    private void EnsureDragonCubes()
    {
        if (disableDragonCubes) return;
        float weight = dragonCubeSpawnWeight > 0f ? dragonCubeSpawnWeight : 0.0001f;
        for (int stars = 1; stars <= 7; stars++)
        {
            PixelType type = (PixelType)((int)PixelType.DragonCube1 + stars - 1);
            if (IndexOf(type) >= 0) continue;
            EnsureTier(new PixelTier
            {
                type = type,
                displayName = "Dragon Cube (" + stars + ")",
                color = new Color(1f, 0.62f, 0.12f, 0.6f),
                amountPerClick = 1,
                spawnWeight = weight,
                rareDrop = true,
                translucent = true,
                spawnSwitch = SpawnSwitch.AlwaysOn,
                unlockMode = TierUnlockMode.ShopOnly,
            });
        }
    }

    [Tooltip("Spawn weight of the Golden Snitch (a very rare drop that flies off the cube). 0 = the coded default (0.00003; a Dragon Cube is 0.0001).")]
    [SerializeField] private float goldenSnitchSpawnWeight = 0f;

    [Min(0.01f)]
    [Tooltip("Rare drops (Dragon / God / Prime Cubes, the Golden Snitch) get a FIXED chance per block = their spawn weight / this number, " +
             "no matter how many pixel types are unlocked. (They used to share the normal weighted roll, so at the start - only White unlocked - " +
             "a Dragon Cube was about 1 in 1,400 blocks instead of 1 in 70,000.) 7 = the odds the weights were chosen for.")]
    [SerializeField] private float rareReferenceWeight = 7f;

    /// <summary>The fixed chance per block of this rare drop (0 if it is not a rare drop or can't spawn).</summary>
    private float RareChance(int i) =>
        IsValidTier(i) && tiers[i].rareDrop && tiers[i].CanSpawn ? SpawnWeightOf(i) / Mathf.Max(0.01f, rareReferenceWeight) : 0f;

    /// <summary>The chance per block that SOME rare drop comes up.</summary>
    private float RareChanceTotal()
    {
        float sum = 0f;
        for (int i = 0; i < tiers.Length; i++) sum += RareChance(i);
        return Mathf.Clamp01(sum);
    }

    /// <summary>Rolls the rare drops on their own, before the normal pick: each has its fixed chance (RareChance).</summary>
    private bool TryPickRare(out int pick)
    {
        pick = -1;
        float roll = UnityEngine.Random.value;
        for (int i = 0; i < tiers.Length; i++)
        {
            float p = RareChance(i);
            if (p <= 0f) continue;
            roll -= p;
            if (roll < 0f) { pick = i; return true; }
        }
        return false;
    }

    /// <summary>The Golden Snitch tier: a very rare drop with no requirements; it never stays on the cube (PixelGoldenSnitch makes it fly).</summary>
    private void EnsureGoldenSnitch()
    {
        if (IndexOf(PixelType.GoldenSnitch) >= 0) return;
        EnsureTier(new PixelTier
        {
            type = PixelType.GoldenSnitch, displayName = "Golden Snitch", color = new Color(1f, 0.78f, 0.22f, 1f), amountPerClick = 1,
            spawnWeight = goldenSnitchSpawnWeight > 0f ? goldenSnitchSpawnWeight : 0.00003f,
            rareDrop = true, glow = true, glowIntensity = 1.8f, spawnSwitch = SpawnSwitch.AlwaysOn, unlockMode = TierUnlockMode.ShopOnly,
        });
    }

    /// <summary>The three Egyptian God Cube tiers (Slifer red, Ra gold, Obelisk blue): 1 payout, an insanely low spawn weight, no requirements, only one of each can be held.</summary>
    private void EnsureGodCubes()
    {
        if (disableGodCubes) return;
        float weight = godCubeSpawnWeight > 0f ? godCubeSpawnWeight : 0.000001f;
        void Add(PixelType type, string name, Color color, float glowIntensity)
        {
            int existing = IndexOf(type);
            if (existing >= 0) { if (PixelClicker.IsInfinityStone(type) && tiers[existing].displayName != name) tiers[existing].displayName = name; return; }   // (a saved scene keeps the old name otherwise)
            EnsureTier(new PixelTier
            {
                type = type, displayName = name, color = color, amountPerClick = 1, spawnWeight = weight,
                rareDrop = true, glow = true, glowIntensity = glowIntensity,
                spawnSwitch = SpawnSwitch.AlwaysOn, unlockMode = TierUnlockMode.ShopOnly,
            });
        }
        Add(PixelType.GodSlifer, "Slifer the Sky Dragon", new Color(0.92f, 0.12f, 0.1f, 1f), 1.6f);
        Add(PixelType.GodRa, "Ra the Winged Dragon", new Color(1f, 0.82f, 0.15f, 1f), 2.2f);
        Add(PixelType.GodObelisk, "Obelisk the Tormentor", new Color(0.2f, 0.38f, 0.95f, 1f), 1.4f);
        // The six Prime Cubes drop like the God Cubes (same weight); the Cubelette is only ever made with the Gauntlet (weight 0 = never rolled).
        for (int i = 0; i < 6; i++) Add(PixelType.StoneSpace + i, PixelInfinity.StoneName(i), PixelInfinity.StoneColor(i), 1.5f);
        int cubeletteAt = IndexOf(PixelType.InfinityCubelette);
        if (cubeletteAt >= 0) tiers[cubeletteAt].displayName = "Prime Cubelette";   // (a saved scene keeps the old name otherwise)
        else
            EnsureTier(new PixelTier
            {
                type = PixelType.InfinityCubelette, displayName = "Prime Cubelette", color = new Color(1f, 0.9f, 0.55f, 1f), amountPerClick = 1, spawnWeight = 0f,
                rareDrop = true, glow = true, glowIntensity = 2.2f, spawnSwitch = SpawnSwitch.AlwaysOn, unlockMode = TierUnlockMode.ShopOnly,
            });
    }

    // ------------------------------------------------------------------
    // Tier helpers
    // ------------------------------------------------------------------

    private bool IsValidTier(int i) => tiers != null && i >= 0 && i < tiers.Length;

    /// <summary>Index of the tier with this currency type, or -1 if there isn't one.</summary>
    public int IndexOf(PixelType type)
    {
        for (int i = 0; i < tiers.Length; i++)
            if (tiers[i].type == type) return i;
        return -1;
    }

    private int GetHighestUnlockedIndex()
    {
        // Shop tiers are skipped so buying them doesn't change the "highest tier" click behaviour.
        for (int i = tiers.Length - 1; i >= 0; i--)
            if (tiers[i].unlocked && tiers[i].unlockMode != TierUnlockMode.ShopOnly) return i;
        return 0;
    }

    /// <summary>Is the tier with this currency type unlocked?</summary>
    public bool IsUnlocked(PixelType type)
    {
        int i = IndexOf(type);
        return i >= 0 && tiers[i].unlocked;
    }

    /// <summary>Unlocks a tier directly (shops, achievements...). Returns false if invalid or already unlocked.</summary>
    public bool UnlockTier(int index)
    {
        if (!IsValidTier(index) || tiers[index].unlocked) return false;
        tiers[index].unlocked = true;
        PlaySound(unlockSound);
        onTierUnlocked?.Invoke(index);
        NotifyChanged();
        return true;
    }

    /// <summary>Dev tools (relocking a shop pack): locks a tier again so it stops spawning. Its count is kept. Returns false if invalid or already locked.</summary>
    public bool LockTier(int index)
    {
        if (!IsValidTier(index) || !tiers[index].unlocked) return false;
        tiers[index].unlocked = false;
        NotifyChanged();
        return true;
    }

    /// <summary>
    /// Makes sure a tier of this type exists, appending a locked copy of <paramref name="definition"/> if not.
    /// Returns its index. Lets the shop add Red/Green/Blue without editing the Tiers list by hand.
    /// </summary>
    public int EnsureTier(PixelTier definition)
    {
        int existing = IndexOf(definition.type);
        if (existing >= 0)
        {
            if (definition.type == PixelType.HornetNest && tiers[existing].displayName == "Hornet Nest") tiers[existing].displayName = "Termite Hive";   // renamed
            // The Tiers list already has this type: still honour a Starting Amount set on the shop's definition.
            if (definition.startingAmount > 0 && tiers[existing].startingAmount <= 0)
            {
                tiers[existing].startingAmount = definition.startingAmount;
                tiers[existing].count = definition.startingAmount;
            }
            return existing;
        }

        PixelTier copy = definition.Clone();
        copy.unlocked = false;
        // Keep Count / Total Collected typed on the shop's definition (as for normal tiers);
        // Starting Amount raises Count if it's higher.
        if (copy.count < copy.startingAmount) copy.count = copy.startingAmount;

        // The Dragon Cubes always stay at the END of the list, so shop tiers added later still follow the normal tiers
        // (the tier guide and the unlock chain rely on that order).
        List<PixelTier> list = new List<PixelTier>(tiers);
        int insertAt = list.Count;
        if (!copy.rareDrop)
            for (int i = 0; i < list.Count; i++)
                if (list[i].rareDrop) { insertAt = i; break; }
        list.Insert(insertAt, copy);
        tiers = list.ToArray();
        return insertAt;
    }

    /// <summary>The Wood Chips tier (made on first use; never spawns on the cube, the Wood Chipper spits them out as old pixels). Returns its index.</summary>
    /// <summary>The Charcoal tier (made on first use; the Kiln spits it out as old pixels, it never spawns on the cube). Returns its index.</summary>
    public int EnsureCharcoalTier()
    {
        return EnsureTier(new PixelTier
        {
            type = PixelType.Charcoal,
            displayName = "Charcoal",
            color = new Color(0.14f, 0.14f, 0.16f, 1f),
            amountPerClick = 1,
            spawnWeight = 0f,
            spawnSwitch = SpawnSwitch.AlwaysOn,
            unlockMode = TierUnlockMode.ShopOnly,
        });
    }

    public int EnsureWoodChipsTier()
    {
        return EnsureTier(new PixelTier
        {
            type = PixelType.WoodChips,
            displayName = "Wood Chips",
            color = new Color(0.82f, 0.62f, 0.36f, 1f),
            amountPerClick = 1,
            spawnWeight = 0f,
            spawnSwitch = SpawnSwitch.AlwaysOn,
            unlockMode = TierUnlockMode.ShopOnly,
        });
    }

    /// <summary>A special block's tier (PixelBlockRules.SpecialRule): made on first use, no spawn weight, unlocked so the cube can show it. Returns its index.</summary>
    public int EnsureSpecialTier(PixelTier definition)
    {
        int index = EnsureTier(definition);
        if (IsValidTier(index)) tiers[index].unlocked = true;
        return index;
    }

    private int[] devSpawnTiers; // dev tools: only these tiers spawn (locked ones too); null = normal spawning

    /// <summary>True while the dev tools restrict spawning to chosen pixel types.</summary>
    public bool HasDevSpawn => devSpawnTiers != null;

    /// <summary>
    /// Dev tools: only the given pixel types spawn (even locked ones; several = chosen at random). Null or empty = back to
    /// normal spawning. The pixel on screen is swapped straight away.
    /// </summary>
    public void SetDevSpawnTiers(PixelType[] types)
    {
        System.Collections.Generic.List<int> list = new System.Collections.Generic.List<int>();
        if (types != null)
            foreach (PixelType type in types)
            {
                int index = IndexOf(type);
                if (index >= 0 && !list.Contains(index)) list.Add(index);
            }

        devSpawnTiers = list.Count > 0 ? list.ToArray() : null;
        if (!randomizeSpawnTier) return;

        bool fine = devSpawnTiers != null ? System.Array.IndexOf(devSpawnTiers, currentTierIndex) >= 0
                                          : IsValidTier(currentTierIndex) && tiers[currentTierIndex].unlocked;
        if (fine) return;
        currentTierIndex = RollTier();
        Materialize(GetClickTier());
    }

    private int forcedTierIndex = -1;

    private bool devSwapped; // dev tools: the pixel on the cube was swapped by hand and stays until it is clicked (even a locked type)

    /// <summary>True while the block on the cube is one that came back from Block Hold's queue (a shuffled block), not a fresh roll. The shuffle bonus uses it to light up the combo block.</summary>
    public bool LiveCameFromQueue { get; private set; }

    private int RollTier()
    {
        devSwapped = false;
        LiveCameFromQueue = false;
        if (IsValidTier(currentTierIndex)) previousTier = currentTierIndex;   // (what the 'Next up' window shows as 'before')
        int outgoingTreeTag = liveTreeTag;
        liveTreeTag = 0;
        while (nextInLine.Count > 0)
        {
            Vector2Int queued = nextInLine[0];   // a block bumped by the hold (PixelBlockHold) is next in line, with the hits it had taken
            float queuedStake = nextInLineStake.Count > 0 ? nextInLineStake[0] : 1f;
            nextInLine.RemoveAt(0);
            if (nextInLineStake.Count > 0) nextInLineStake.RemoveAt(0);
            if (!IsValidTier(queued.x)) continue;
            pendingHits = queued.y;
            pendingStake = queuedStake;
            LiveCameFromQueue = true;
            PixelTreeMode.OnLiveChanged(outgoingTreeTag, 0);
            return queued.x;
        }
        int pick, tag;
        TrimUpcoming();
        if (upcoming.Count > 0) { pick = upcoming[0].x; tag = upcoming[0].y; upcoming.RemoveAt(0); }   // the block the 'Next up' window showed (if it was still valid)
        else { pick = PickSpawnTier(); tag = PixelTreeMode.TakeTag(); }
        PixelTreeMode.OnLiveChanged(outgoingTreeTag, tag);   // tree mode: the tree bar counts live tree blocks, Timber when the last one leaves
        liveTreeTag = tag;
        return pick;
    }

    private int liveTreeTag;   // tree mode (PixelTreeMode): the tag of the block on the cube

    // ---- Block Hold (PixelBlockHold.cs) ----

    private readonly System.Collections.Generic.List<Vector2Int> nextInLine = new System.Collections.Generic.List<Vector2Int>();   // (tier, hits) of blocks waiting to come back
    private readonly System.Collections.Generic.List<float> nextInLineStake = new System.Collections.Generic.List<float>();   // the stake multiplier each waiting block carries (parallel to nextInLine; 1 = none)
    private float pendingStake = 1f, liveStake = 1f;
    private int pendingHits;   // hits the next materialized block starts with (a held block keeps its damage)

    /// <summary>Block Hold's stake on the block on the cube: its payout is multiplied by this (1 = no stake, 0 = the gamble was lost).</summary>
    public float LiveStake => liveStake;

    /// <summary>A block carrying a stake (not 1) just appeared on the cube (its multiplier).</summary>
    public event System.Action<float> StakeAppeared;

    /// <summary>A block was put on the cube (its tier index): fired after every Materialize (rolls, swaps, potions...). The Golden Snitch watches for itself here.</summary>
    public event System.Action<int> PixelMaterialized;

    /// <summary>The block on the cube is replaced by a fresh roll at once (the Golden Snitch flew off it).</summary>
    public void ReplaceCurrentPixel()
    {
        if (!randomizeSpawnTier || pixelTransform == null) return;
        hitsOnCurrentPixel = 0;
        devSwapped = false;
        currentTierIndex = RollTier();
        Materialize(GetClickTier());
    }
    // The pre-rolled blocks (tier, tree tag), in the order they will come - only rolled while the 'Next up' window asks for them (PeekTierAhead), so normal play rolls as before.
    private readonly System.Collections.Generic.List<Vector2Int> upcoming = new System.Collections.Generic.List<Vector2Int>();
    private int previousTier = -1;   // the block that was on the cube before this one

    /// <summary>Forgets every pre-rolled block (the rules changed, e.g. Fishing's bait must be seen by the next roll).</summary>
    private void ClearUpcoming() => upcoming.Clear();

    /// <summary>Is this pre-rolled block still a legal spawn (a potion, skill or switch may have changed the rules since)?</summary>
    private bool UpcomingValid(int tier)
    {
        if (!IsValidTier(tier) || devSpawnTiers != null) return false;
        if (IsValidTier(forcedTierIndex) && tiers[forcedTierIndex].unlocked)
            return tier == forcedTierIndex || tier == forcedTierIndex2 || tier == forcedTierIndex3;
        if (IsHazard(tiers[tier].type) && PixelFocus.HoldsOffHazardsNow()) return false;   // a focus potion drunk after the roll: the pre-rolled hazard is dropped
        return tiers[tier].CanSpawn && !PixelSkills.LeftOutOfSpawns(tiers[tier].type);
    }

    /// <summary>Drops the pre-rolled blocks from the first one that is no longer legal (the ones after it were rolled under the old rules too).</summary>
    private void TrimUpcoming()
    {
        for (int i = 0; i < upcoming.Count; i++)
            if (!UpcomingValid(upcoming[i].x)) { upcoming.RemoveRange(i, upcoming.Count - i); return; }
    }

    /// <summary>The block that comes after this one (see PeekTierAhead).</summary>
    public int PeekNextTier() => PeekTierAhead(0);

    /// <summary>The block 'ahead' places after the one on the cube (0 = next): blocks waiting in line from Block Hold first, then the pre-rolled ones (rolled now if
    /// needed, and RollTier hands out exactly these, so the preview is true). -1 when the cube does not roll random blocks.</summary>
    public int PeekTierAhead(int ahead)
    {
        if (!randomizeSpawnTier || tiers == null || tiers.Length == 0 || ahead < 0) return -1;
        int queued = 0;
        for (int i = 0; i < nextInLine.Count; i++)
            if (IsValidTier(nextInLine[i].x)) { if (queued == ahead) return nextInLine[i].x; queued++; }
        int want = ahead - queued;
        TrimUpcoming();
        while (upcoming.Count <= want && upcoming.Count < 8) upcoming.Add(new Vector2Int(PickSpawnTier(), PixelTreeMode.TakeTag()));
        return want < upcoming.Count ? upcoming[want].x : -1;
    }

    /// <summary>The block that was on the cube before the current one (-1 = none yet).</summary>
    public int PreviousTierIndex => IsValidTier(previousTier) ? previousTier : -1;

    /// <summary>Where the block on the cube is.</summary>
    public Vector3 LiveWorldPosition => pixelTransform != null ? pixelTransform.position : transform.position;

    /// <summary>Block Hold: pulls the block on the cube out of the rotation (the cube rolls a new one at once). False if this block can't be held right now.</summary>
    public bool TryPullLive(out int heldTier, out int heldHits)
    {
        heldTier = -1;
        heldHits = 0;
        if (!randomizeSpawnTier || pixelTransform == null || tiers == null || tiers.Length == 0 || isSpawning || CubeHidden || fishingBusy) return false;
        int index = GetClickTierIndex();
        PixelType type = tiers[index].type;
        if (IsHazard(type) || type == PixelType.Bottle || type == PixelType.Whale || type == PixelType.Fish || type == PixelType.Treasure || type == PixelType.GoldenSnitch) return false;
        heldTier = index;
        heldHits = hitsOnCurrentPixel;   // a held block keeps the hits it had
        currentTierIndex = RollTier();
        Materialize(GetClickTier());
        return true;
    }

    /// <summary>Block Hold: puts a held block back at once, with the hits it had. The block that was on the cube (hits and all) moves to next in line.</summary>
    public void ReturnHeldTier(int tierIndex, int hits, float stake = 1f)
    {
        if (!IsValidTier(tierIndex)) return;
        // The held block comes back AFTER the block that is on the cube now (it goes first in line, hits kept). That way a hazard that came up while you held can be dealt with first,
        // and picking a block up and putting it down can never reroll the cube: the two blocks just keep taking turns.
        nextInLine.Insert(0, new Vector2Int(tierIndex, hits));
        nextInLineStake.Insert(0, stake);
        while (nextInLine.Count > 3) { nextInLine.RemoveAt(nextInLine.Count - 1); if (nextInLineStake.Count > 0) nextInLineStake.RemoveAt(nextInLineStake.Count - 1); }
        // (the pre-rolled blocks stay: they now simply come after the waiting ones, so holding never rerolls what the Next up window showed)
    }

    /// <summary>Takes one waiting copy of this block out of the line (Block Hold's queue). True if there was one.</summary>
    public bool DropQueuedTier(int tierIndex)
    {
        for (int i = 0; i < nextInLine.Count; i++)
            if (nextInLine[i].x == tierIndex) { nextInLine.RemoveAt(i); if (i < nextInLineStake.Count) nextInLineStake.RemoveAt(i); return true; }
        return false;
    }

    /// <summary>Dev tools: replaces the pixel on the cube with this type at once (even a locked one) so its behaviour can be tested.</summary>
    public void DevSwapCurrentPixel(PixelType type)
    {
        int index = IndexOf(type);
        if (index < 0 || pixelTransform == null) return;
        if (!randomizeSpawnTier) { activeTierIndex = index; }
        currentTierIndex = index;
        devSwapped = true;
        Materialize(tiers[index]);
    }

    /// <summary>Skill switch test block: the pixel on the cube is replaced by a Glass pixel at once (it stays until it is clicked, like a dev swap). Called when a skill is chosen.</summary>
    public void ShowSkillSwitchBlock()
    {
        int index = IndexOf(PixelType.Glass);
        if (index < 0 || pixelTransform == null || !randomizeSpawnTier) return;
        currentTierIndex = index;
        devSwapped = true;
        Materialize(tiers[index]);
        PixelSkillSwitchFx.Play(pixelTransform, PixelBaseSize);   // it spins and flashes as it appears
    }

    /// <summary>True while a potion (or other effect) restricts spawning to one pixel type.</summary>
    public bool HasForcedSpawnTier => forcedTierIndex >= 0;

    /// <summary>
    /// Makes only this pixel type spawn until <see cref="ClearForcedSpawnTier"/> is called. The pixel
    /// currently shown is swapped for it straight away. Ignored if the tier is missing or locked.
    /// </summary>
    public void SetForcedSpawnTier(PixelType type)
    {
        int index = IndexOf(type);
        if (index < 0 || !tiers[index].unlocked) return;

        forcedTierIndex = index;
        if (randomizeSpawnTier && currentTierIndex != index)
        {
            currentTierIndex = index;
            Materialize(GetClickTier());
        }
    }

    private int forcedTierIndex2 = -1, forcedTierIndex3 = -1;

    /// <summary>
    /// Like <see cref="SetForcedSpawnTier"/> but for two or three pixel types (a combo potion): each new pixel is one of them, evenly.
    /// Only the unlocked ones count.
    /// </summary>
    public void SetForcedSpawnTiers(PixelType a, PixelType b, PixelType? c = null)
    {
        int first = -1, second = -1, third = -1;
        void Take(PixelType type)
        {
            int i = IndexOf(type);
            if (i < 0 || !tiers[i].unlocked || i == first || i == second || i == third) return;
            if (first < 0) first = i; else if (second < 0) second = i; else third = i;
        }
        Take(a); Take(b);
        if (c.HasValue) Take(c.Value);
        if (first < 0) return;

        forcedTierIndex = first;
        forcedTierIndex2 = second;
        forcedTierIndex3 = third;

        if (randomizeSpawnTier && currentTierIndex != forcedTierIndex && currentTierIndex != forcedTierIndex2 && currentTierIndex != forcedTierIndex3)
        {
            currentTierIndex = RollTier();
            Materialize(GetClickTier());
        }
    }

    /// <summary>Back to the normal weighted random spawning.</summary>
    public void ClearForcedSpawnTier()
    {
        forcedTierIndex = -1;
        forcedTierIndex2 = -1;
        forcedTierIndex3 = -1;
        potionVacuumCounter = 0;
    }

    /// <summary>Should this Vacuum click vacuum? Always, unless a potion is forcing this tier - then only every Nth click.</summary>
    private bool VacuumThisClick(int tierIndex)
    {
        bool forcedByPotion = forcedTierIndex >= 0 && (tierIndex == forcedTierIndex || tierIndex == forcedTierIndex2 || tierIndex == forcedTierIndex3);
        if (!forcedByPotion || PotionVacuumEvery <= 1) return true;
        potionVacuumCounter++;
        return potionVacuumCounter % PotionVacuumEvery == 0;
    }

    /// <summary>Weighted random pick among unlocked tiers (or the forced tier while a potion is active).</summary>
    /// <summary>A tier's spawn weight, multiplied while its pet is active (PixelPets).</summary>
    /// <summary>True when this pixel type can come up on the cube right now (unlocked, not switched off, and in the rotation of the skill pack in play).</summary>
    public bool InRotation(int i)
    {
        if (!IsValidTier(i)) return false;
        if (tiers[i].rareDrop) return tiers[i].count > 0d || tiers[i].totalCollected > 0d;   // a rare drop is listed once found
        return tiers[i].CanSpawn && !PixelSkills.LeftOutOfSpawns(tiers[i].type);
    }

    /// <summary>The share (0..1) of the cube's pixels that are this type; 0 when it is not in the rotation.</summary>
    public float SpawnShare(int i)
    {
        if (!IsValidTier(i) || !tiers[i].CanSpawn || PixelSkills.LeftOutOfSpawns(tiers[i].type)) return 0f;
        return PixelBlockRules.AdjustShare(this, i, SpawnShareBase(i));   // (special blocks take their chance off the blocks they replace)
    }

    /// <summary>The share before the special-block replacements are worked in.</summary>
    public float SpawnShareBase(int i)
    {
        if (!IsValidTier(i) || !tiers[i].CanSpawn || PixelSkills.LeftOutOfSpawns(tiers[i].type)) return 0f;
        if (tiers[i].rareDrop) return RareChance(i);   // a rare drop's own fixed chance
        return (1f - RareChanceTotal()) * SpawnShareNonRare(i);   // everything else shares what the rare roll leaves
    }

    private float SpawnShareNonRare(int i)
    {
        // The pack's fillers (Twig, Pinecone, Moss) split the filler's share evenly.
        if (PixelSkills.GetFillerFamily(fillerFamilyScratch) && fillerFamilyScratch.Contains(tiers[i].type))
        {
            int count = 0, primary = IndexOf(fillerFamilyScratch[0]);
            foreach (PixelType t in fillerFamilyScratch) { int k = IndexOf(t); if (k >= 0 && tiers[k].CanSpawn && !PixelSkills.LeftOutOfSpawns(t)) count++; }
            return primary >= 0 && count > 0 ? SpawnShareCore(primary) / count : 0f;
        }
        return SpawnShareCore(i);
    }

    private float SpawnShareCore(int i)
    {
        if (TryFishingShare(i, out float fishShare)) return fishShare;
        if (PixelSkills.TryGetUniformPool(this, uniformPool)) return uniformPool.Contains(i) ? 1f / uniformPool.Count : 0f;   // a truly random rotation
        bool[] ok = FillSpawnableNow();
        SpecialInfo(ok, out int fillerIndex, out float fillerWeight, out int hazardIndex, out float hazardWeight);
        float total = 0f;
        for (int k = 0; k < tiers.Length; k++)
            if (ok[k] && !tiers[k].rareDrop) total += k == fillerIndex ? fillerWeight : k == hazardIndex ? hazardWeight : SpawnWeightOf(k);
        float mine = i == fillerIndex ? fillerWeight : i == hazardIndex ? hazardWeight : SpawnWeightOf(i);
        return total > 0f ? mine / total : 0f;
    }

    private float SpawnWeightOf(int i) => Mathf.Max(0f, tiers[i].spawnWeight) * PixelPets.SpawnMultiplier(tiers[i].type) * (tiers[i].type == PixelType.Fish ? PixelBait.FishMultiplier : 1f);

    /// <summary>
    /// The skill pack in play may have a FILLER block (Dirt, Stone: a set share of the spawns, bigger at low skill level) and a HAZARD block
    /// (Pest, Gas Pocket: a small share that grows with level). Returns their tier indexes (or -1) and the weights that give them those shares.
    /// </summary>
    private void SpecialInfo(bool[] ok, out int fillerIndex, out float fillerWeight, out int hazardIndex, out float hazardWeight)
    {
        fillerIndex = hazardIndex = -1;
        fillerWeight = hazardWeight = 0f;
        int f = -1, h = -1;
        float fShare = 0f, hShare = 0f;
        if (PixelSkills.TryGetFiller(out PixelType ft, out float fs) && fs > 0f) { f = IndexOf(ft); fShare = fs; }
        if (PixelSkills.TryGetHazard(out PixelType ht, out float hs) && hs > 0f) { h = IndexOf(ht); hShare = hs; }
        if (f >= 0 && !ok[f]) f = -1;
        if (h >= 0 && !ok[h]) h = -1;
        if (f < 0 && h < 0) return;
        float others = 0f;
        for (int k = 0; k < tiers.Length; k++)
            if (k != f && k != h && ok[k] && !tiers[k].rareDrop) others += SpawnWeightOf(k);   // (rare drops roll on their own)
        if (others <= 0f) return;
        float basis = others;
        if (f >= 0) { fillerIndex = f; fillerWeight = others * fShare / (1f - fShare); basis += fillerWeight; }
        if (h >= 0) { hazardIndex = h; hazardWeight = basis * hShare / (1f - hShare); }
    }

    private int PickSpawnTier()
    {
        PixelTreeMode.TakeTag();   // (clears a stale tag)
        // Rare drops first, at a fixed chance per block (not weighted against whatever else is unlocked); never while dev spawning or a potion forces a type.
        if (devSpawnTiers == null && !(IsValidTier(forcedTierIndex) && tiers[forcedTierIndex].unlocked) && TryPickRare(out int rarePick)) return rarePick;
        // Tree mode (Woodcutting with the Oak Trees test pack): filler rotation and trees in their designed order; no filler swap or special-block replacement.
        if (devSpawnTiers == null && !(IsValidTier(forcedTierIndex) && tiers[forcedTierIndex].unlocked) && PixelTreeMode.TryPick(this, out int treePick)) return treePick;
        int pick = PickSpawnTierBase();
        // Safety net: a block that is locked or belongs to a skill that is not in play must never come up on its own (dev spawning and forced blocks are exempt).
        if (devSpawnTiers == null && IsValidTier(pick) && !(IsValidTier(forcedTierIndex) && tiers[forcedTierIndex].unlocked)
            && (!tiers[pick].CanSpawn || PixelSkills.LeftOutOfSpawns(tiers[pick].type)) && !IsRareCube(tiers[pick].type))
        {
            PixelSkills sk = PixelSkills.Instance;
            Debug.LogWarning("PixelClicker: the spawn roll picked '" + tiers[pick].displayName + "' (" + tiers[pick].type + ") but it may not spawn now (unlocked " + tiers[pick].unlocked
                             + ", spawnDisabled " + tiers[pick].spawnDisabled + ", skill in play " + (sk != null ? sk.ActiveId : "none") + ") - rolled again.", this);
            for (int attempt = 0; attempt < 8; attempt++)
            {
                int again = PickSpawnTierBase();
                if (IsValidTier(again) && tiers[again].CanSpawn && !PixelSkills.LeftOutOfSpawns(tiers[again].type)) { pick = again; break; }
            }
            if (!tiers[pick].CanSpawn || PixelSkills.LeftOutOfSpawns(tiers[pick].type)) pick = GetHighestUnlockedIndex();
        }
        pick = SwapFiller(pick);   // the filler share is split between the pack's fillers (Woodcutting: Twig, Pinecone, Moss)
        return PixelBlockRules.TryReplace(this, pick);   // a rare special block (Great Oak...) may replace it
    }

    /// <summary>When the roll lands on the pack's main filler, one of its filler FAMILY (the main one and the pack's extra fillers that can spawn) is picked at random.</summary>
    private int SwapFiller(int pick)
    {
        if (!IsValidTier(pick) || devSpawnTiers != null || !PixelSkills.GetFillerFamily(fillerFamilyScratch) || !fillerFamilyScratch.Contains(tiers[pick].type)) return pick;
        int count = 0, chosen = pick;
        foreach (PixelType t in fillerFamilyScratch)
        {
            int i = IndexOf(t);
            if (i < 0 || !tiers[i].CanSpawn || PixelSkills.LeftOutOfSpawns(t)) continue;
            count++;
            if (UnityEngine.Random.value < 1f / count) chosen = i;
        }
        return chosen;
    }

    private bool[] spawnableScratch = new bool[0];

    /// <summary>
    /// For each tier: can it spawn right now (CanSpawn and not left out by the skill pack in play)? Worked out ONCE per roll and shared by
    /// SpecialInfo and the weight loops, which used to ask PixelSkills.LeftOutOfSpawns three times per tier per click. The array is reused: read it before the next call.
    /// </summary>
    private bool[] FillSpawnableNow()
    {
        if (spawnableScratch.Length != tiers.Length) spawnableScratch = new bool[tiers.Length];
        for (int i = 0; i < tiers.Length; i++) spawnableScratch[i] = tiers[i].CanSpawn && !PixelSkills.LeftOutOfSpawns(tiers[i].type);
        return spawnableScratch;
    }

    private readonly System.Collections.Generic.List<PixelType> fillerFamilyScratch = new System.Collections.Generic.List<PixelType>();
    private readonly System.Collections.Generic.List<int> uniformPool = new System.Collections.Generic.List<int>();

    private int PickSpawnTierBase()
    {
        if (devSpawnTiers != null) return devSpawnTiers[UnityEngine.Random.Range(0, devSpawnTiers.Length)];

        if (IsValidTier(forcedTierIndex) && tiers[forcedTierIndex].unlocked)
        {
            int pick = forcedTierIndex, count = 1;
            if (IsValidTier(forcedTierIndex2) && tiers[forcedTierIndex2].unlocked) { count++; if (UnityEngine.Random.value < 1f / count) pick = forcedTierIndex2; }
            if (IsValidTier(forcedTierIndex3) && tiers[forcedTierIndex3].unlocked) { count++; if (UnityEngine.Random.value < 1f / count) pick = forcedTierIndex3; }
            return pick;
        }

        // A skill pack in play (Mining, Farming): its pixels replace the basic pixels (White..Blue, Glass, Luminescent) in what spawns; the pixels of
        // packs that are not in play stay out unless the player turned them on as guests (a skill tree unlock).
        if (TryPickFishing(out int fishPick)) return fishPick;   // Fishing has its own fixed table
        if (PixelSkills.TryGetUniformPool(this, uniformPool)) return uniformPool[UnityEngine.Random.Range(0, uniformPool.Count)];   // a truly random rotation (Foraging)
        bool[] ok = FillSpawnableNow();
        SpecialInfo(ok, out int fillerIndex, out float fillerWeight, out int hazardIndex, out float hazardWeight);
        float total = 0f;
        for (int i = 0; i < tiers.Length; i++)
            if (ok[i] && !tiers[i].rareDrop) total += i == fillerIndex ? fillerWeight : i == hazardIndex ? hazardWeight : SpawnWeightOf(i);

        if (total <= 0f) return GetHighestUnlockedIndex();

        float roll = UnityEngine.Random.value * total;
        for (int i = 0; i < tiers.Length; i++)
        {
            if (!ok[i] || tiers[i].rareDrop) continue;   // (rare drops have their own roll: TryPickRare)
            roll -= i == fillerIndex ? fillerWeight : i == hazardIndex ? hazardWeight : SpawnWeightOf(i);
            if (roll <= 0f) return i;
        }
        return GetHighestUnlockedIndex();
    }

    /// <summary>Rolls the next pixel again now (used when the Prospector mode switch changes).</summary>
    public void RefreshSpawnTier()
    {
        if (!randomizeSpawnTier || pixelTransform == null) return;
        currentTierIndex = RollTier();
        Materialize(GetClickTier());
    }

    private int GetClickTierIndex()
    {
        if (randomizeSpawnTier && (devSpawnTiers != null || devSwapped) && IsValidTier(currentTierIndex)) return currentTierIndex; // dev tools: even locked tiers
        if (randomizeSpawnTier)
            return IsValidTier(currentTierIndex) && (tiers[currentTierIndex].unlocked || tiers[currentTierIndex].rareDrop)
                ? currentTierIndex
                : GetHighestUnlockedIndex();

        if (autoUseHighestTier) return GetHighestUnlockedIndex();
        return IsValidTier(activeTierIndex) && tiers[activeTierIndex].unlocked
            ? activeTierIndex
            : GetHighestUnlockedIndex();
    }

    private PixelTier GetClickTier() => tiers[GetClickTierIndex()];
}
