// ---- System notes ----
// PixelClickerSkillBlocks.cs
//   Part of partial class PixelClicker: the core's side of the skill / special blocks - Solar clusters and seed boosts, plants and seed
//   sprouts (RandomPlantPixelTier, RandomSeedPixelTier, PlantSeedSproutAt, SpawnSeedSprout), the farming blocks (Pollen, Weevil, Fertilize,
//   Wheat / Golden Fruit settings), Water splash pieces, the Electrician's conductors and capacitor, the Treasure chest (OpenTreasure,
//   TreasureFountain) and Fishing (its settings, Bottle / Whale / GiveFishingHaul). Most of each block's own behaviour lives in its own
//   script (PixelFarmFx, PixelElectricFx, PixelTreasure, PixelFishingSpawns...). Split out of PixelClicker.cs (no behaviour change).
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
    private float solarClusterTimer;
    private readonly System.Collections.Generic.List<Rigidbody> solarBodies = new System.Collections.Generic.List<Rigidbody>();
    private readonly System.Collections.Generic.List<OldPixelDespawn> solarDespawns = new System.Collections.Generic.List<OldPixelDespawn>();
    private readonly System.Collections.Generic.List<int> solarGroup = new System.Collections.Generic.List<int>();
    private bool[] solarSeen = new bool[0];

    /// <summary>
    /// Old Solar pixels that touch each other in a group of <see cref="solarClusterSize"/> or more are kept alive (they never age
    /// or despawn while the group is complete). Checked a few times a second; the keep-alive lasts a little longer than that.
    /// </summary>
    private void UpdateSolarClusters()
    {
        if (solarClusterSize < 2 || Time.timeScale <= 0f) return;
        solarClusterTimer -= Time.unscaledDeltaTime;
        if (solarClusterTimer > 0f) return;
        solarClusterTimer = 0.15f;

        int solar = IndexOf(PixelType.Solar);
        if (solar < 0 || !tiers[solar].unlocked) return;   // (no Solar husks can drop while Solar is locked: skip the scan)
        solarBodies.Clear();
        solarDespawns.Clear();
        foreach (Rigidbody rb in oldPixels)
        {
            if (rb == null) continue;
            PixelHuskQuery.Husk h = PixelHuskQuery.Get(rb);
            OldPixelInfo info = h.info;
            if (info == null || info.tierIndex != solar) continue;
            OldPixelDespawn d = h.despawn;
            if (d == null || d.IsDespawning) continue;
            solarBodies.Add(rb);
            solarDespawns.Add(d);
        }
        int n = solarBodies.Count;
        if (n < solarClusterSize) return;

        float reach = OldPixelWorldSize * solarClusterDistance, reachSqr = reach * reach;
        if (solarSeen.Length < n) solarSeen = new bool[n * 2];
        for (int i = 0; i < n; i++) solarSeen[i] = false;
        for (int start = 0; start < n; start++)
        {
            if (solarSeen[start]) continue;
            solarGroup.Clear();
            solarGroup.Add(start);
            solarSeen[start] = true;
            for (int g = 0; g < solarGroup.Count; g++)
            {
                Vector3 from = solarBodies[solarGroup[g]].position;
                for (int j = 0; j < n; j++)
                {
                    if (solarSeen[j] || (solarBodies[j].position - from).sqrMagnitude > reachSqr) continue;
                    solarSeen[j] = true;
                    solarGroup.Add(j);
                }
            }
            if (solarGroup.Count < solarClusterSize) continue;
            foreach (int member in solarGroup)
            {
                solarDespawns[member].KeepAlive();
                OldPixelSolarAura aura = solarBodies[member].GetComponent<OldPixelSolarAura>();
                if (aura != null) aura.Keep();   // a complete group glows
            }
        }
    }

    /// <summary>How big a Seed sprout is drawn, in world units (an old pixel's width x the sprout scale).</summary>
    public float SeedSproutWorldSize => OldPixelWorldSize * seedSproutScale;

    /// <summary>The 'plant' pixels: what the seedlings of a cracked Seed shell grow (Wheat, Pollen, Golden Fruit and Wood).</summary>
    public static bool IsPlant(PixelType type) => PixelBlockTags.Has(type, PixelTag.Plant);   // (the tag table in PixelBlockTags.cs)

    /// <summary>A random unlocked plant pixel (by spawn weight, so Golden Fruit stays rare); falls back to any growable type when no plant is unlocked yet. -1 if none.</summary>
    public int RandomPlantPixelTier()
    {
        float total = 0f;
        for (int i = 0; i < tiers.Length; i++)
            if (tiers[i].unlocked && IsPlant(tiers[i].type)) total += Mathf.Max(0.01f, tiers[i].spawnWeight);
        if (total <= 0f) return RandomSeedPixelTier();
        float pick = UnityEngine.Random.value * total;
        int last = -1;
        for (int i = 0; i < tiers.Length; i++)
        {
            if (!tiers[i].unlocked || !IsPlant(tiers[i].type)) continue;
            last = i;
            pick -= Mathf.Max(0.01f, tiers[i].spawnWeight);
            if (pick <= 0f) return i;
        }
        return last;
    }

    /// <summary>A random pixel type a Seed sprout can grow: unlocked, not a rare drop, not a fly-away type, not a Seed itself and never a Vacuum or Singularity pixel (those can still be planted from bought seeds). -1 if none.</summary>
    public int RandomSeedPixelTier()
    {
        // Golden Fruit: a rare treat from a fully grown sprout (once that type is unlocked).
        int fruit = IndexOf(PixelType.GoldenFruit);
        if (fruit >= 0 && tiers[fruit].unlocked && UnityEngine.Random.value < (fruitSproutChance > 0f ? fruitSproutChance : 0.03f)) return fruit;

        // Rarer types (lower spawn weight) are likelier: chance ~ 1 / weight^bias.
        System.Collections.Generic.List<int> candidates = new System.Collections.Generic.List<int>();
        System.Collections.Generic.List<float> weights = new System.Collections.Generic.List<float>();
        float total = 0f;
        for (int i = 0; i < tiers.Length; i++)
        {
            PixelTier t = tiers[i];
            if (!t.unlocked || t.rareDrop || t.flyAway || t.type == PixelType.Seed || t.type == PixelType.Singularity || t.type == PixelType.Vacuum || IsStandalone(t.type)) continue;
            float w = 1f / Mathf.Pow(Mathf.Max(0.02f, t.spawnWeight), seedRarityBias);
            candidates.Add(i);
            weights.Add(w);
            total += w;
        }
        if (candidates.Count == 0) return -1;
        float pick = UnityEngine.Random.value * total;
        for (int n = 0; n < candidates.Count; n++)
        {
            pick -= weights[n];
            if (pick <= 0f) return candidates[n];
        }
        return candidates[candidates.Count - 1];
    }

    /// <summary>Extra growth speed (0 = none) a sprout or Seed pet at 'position' gets from old Solar pixels lying close to it.</summary>
    public float SolarSeedBoostAt(Vector3 position)
    {
        int solar = IndexOf(PixelType.Solar), dirt = IndexOf(PixelType.Dirt);
        float solarEach = solar >= 0 ? seedSolarBoostEach : 0f;
        float dirtEach = dirt >= 0 ? (dirtSeedBoost > 0f ? dirtSeedBoost : 0.2f) : 0f;   // old Dirt pixels help sprouts a little too
        if (solarEach <= 0f && dirtEach <= 0f) return 0f;
        float reach = OldPixelWorldSize * seedSolarReach, reachSqr = reach * reach;
        float boost = 0f;
        foreach (Rigidbody rb in oldPixels)
        {
            if (rb == null || (rb.position - position).sqrMagnitude > reachSqr) continue;
            OldPixelInfo info = rb.GetComponent<OldPixelInfo>();
            if (info == null) continue;
            if (info.tierIndex == solar) boost += solarEach;
            else if (info.tierIndex == dirt) boost += dirtEach;
        }
        return Mathf.Min(seedSolarBoostMax, boost) + PixelDaylight.GrowthBonus;   // daylight (raised by clicking Solar) speeds growth everywhere
    }

    // ---- Farming pixels ----

    [Header("Farming Pixels")]
    [Tooltip("Growth speed an old Dirt pixel near a sprout adds (counts towards the same cap as Solar pixels). 0 = 0.2.")]
    [SerializeField] private float dirtSeedBoost = 0f;

    [Tooltip("Tick to turn off the farming blocks' hit and break particles (the ambient particles come from their looks).")]
    [SerializeField] private bool disableFarmFx = false;

    [Tooltip("Wheat: extra payout per growing sprout when you click it (0.25 = +25% each). 0 = 0.25.")]
    [SerializeField] private float wheatPerSprout = 0f;

    [Tooltip("Wheat: at most this many sprouts count. 0 = 8.")]
    [SerializeField] private int wheatMaxSprouts = 0;

    [Tooltip("Pollen: an old Pollen pixel within this many main-pixel widths of a sprout makes its grown pixel pay double. 0 = 3.")]
    [SerializeField] private float pollenReachPixels = 0f;

    [Tooltip("Golden Fruit: chance that a sprout grows a Golden Fruit instead of a random pixel (once Golden Fruit is unlocked). 0 = 3%.")]
    [SerializeField] private float fruitSproutChance = 0f;

    [Tooltip("Weevil: seconds between bites while a Weevil is on the cube (each bite eats one growing sprout until you click it). 0 = 2.5.")]
    [SerializeField] private float weevilBiteSeconds = 0f;

    private float weevilTimer;
    private bool weevilTold;

    /// <summary>Is an old Pollen pixel lying close to this spot (a sprout popping off here pays double)?</summary>
    public bool PollenNear(Vector3 position)
    {
        int pollen = IndexOf(PixelType.Pollen);
        if (pollen < 0) return false;
        float reach = PixelBaseSize * (pollenReachPixels > 0f ? pollenReachPixels : 3f), reachSqr = reach * reach;
        foreach (Rigidbody rb in oldPixels)
        {
            if (rb == null || (rb.position - position).sqrMagnitude > reachSqr) continue;
            OldPixelInfo info = rb.GetComponent<OldPixelInfo>();
            if (info != null && info.tierIndex == pollen) return true;
        }
        return false;
    }

    /// <summary>While a Weevil is the pixel on the cube it bites a growing sprout every few seconds until you click it.</summary>
    private void UpdateWeevil()
    {
        if (!IsValidTier(currentTierIndex) || tiers[currentTierIndex].type != PixelType.Weevil || isSpawning || pixelTransform == null) { weevilTimer = 0f; return; }
        weevilTimer += Time.deltaTime;
        if (weevilTimer < (weevilBiteSeconds > 0f ? weevilBiteSeconds : 2.5f)) return;
        weevilTimer = 0f;
        if (SeedSprout.EatOne(out Vector3 at))
        {
            SeedDigFx.Play(at, PixelBaseSize * 0.6f, 0.5f);
            if (!weevilTold) { weevilTold = true; PixelHints.Announce(weevilText); }
        }
    }

    /// <summary>Fertilizer: every growing sprout and Seed pet on the field jumps ahead at once.</summary>
    private void Fertilize()
    {
        WaterSplashAt(pixelTransform.position, 1000f);
        SeedDigFx.Play(pixelTransform.position, PixelBaseSize, 0.8f);
    }

    /// <summary>How long a sprout / Seed pet stays sun soaked after old Solar pixels were last near it.</summary>
    public float SeedSunSoakSeconds => seedSunSoakSeconds;

    [Header("Water splash pieces")]
    [Tooltip("Turn the splash pieces off: an old Water pixel then just puffs into droplets where it lands, as before.")]
    [SerializeField] private bool disableWaterPieces = false;

    [Min(0)]
    [Tooltip("How many smaller Water pixels an old Water pixel breaks into when it splashes. 0 = 4.")]
    [SerializeField] private int waterPieceCount = 0;

    [Min(0f)]
    [Tooltip("Size of each piece as a fraction of the old Water pixel. 0 = 0.4.")]
    [SerializeField] private float waterPieceSize = 0f;

    [Min(0f)]
    [Tooltip("How fast the pieces are flung out (sideways; they go up about as fast). 0 = 2.2.")]
    [SerializeField] private float waterPieceSpeed = 0f;

    [Min(0f)]
    [Tooltip("Longest a piece flies before it breaks into droplets anyway (seconds); it also breaks the moment it lands. 0 = 1.2.")]
    [SerializeField] private float waterPieceSeconds = 0f;

    public int WaterPieces => disableWaterPieces ? 0 : (waterPieceCount > 0 ? waterPieceCount : 4);
    public float WaterPieceSize => waterPieceSize > 0f ? waterPieceSize : 0.4f;
    public float WaterPieceSpeed => waterPieceSpeed > 0f ? waterPieceSpeed : 2.2f;
    public float WaterPieceSeconds => waterPieceSeconds > 0f ? waterPieceSeconds : 1.2f;

    /// <summary>Minimum seconds between two waterings of the same sprout / Seed pet.</summary>
    public float SeedWaterCooldown => seedWaterCooldown;

    /// <summary>A Water pixel splashed at 'point': every growing sprout and the Seed pet within reach gets an instant bit of growth (rate-limited per target).</summary>
    public void WaterSplashAt(Vector3 point, float reachOverride = 0f)
    {
        float reach = reachOverride > 0f ? reachOverride : OldPixelWorldSize * seedWaterReach;
        SeedSprout.WaterAll(point, reach, seedWaterGrowSeconds, seedWaterCooldown);
        PixelPets.WaterPets(point, reach, seedWaterGrowSeconds, seedWaterCooldown);
    }

    /// <summary>The digging sound and a burst of dirt and dust falling from the shell; 'progress' 0-1 = how close it is to cracking.</summary>
    private void SeedDigEffect(float progress)
    {
        if (pixelTransform == null) return;
        PixelAudio.Play("seed_dig");
        SeedDigFx.Play(pixelTransform.position, PixelBaseSize, Mathf.Clamp01(0.25f + progress * 0.75f));
    }

    /// <summary>The least distance between two planted seeds (world units).</summary>
    public float SeedSpacing => seedSpacing > 0f ? seedSpacing : SeedSproutWorldSize * 0.7f;

    /// <summary>Is there room to plant a seed here? False when another sprout (or a planted Seed pet) is too close - seeds can't sit on top of each other.</summary>
    public bool SeedSpotFree(Vector3 position)
    {
        float r = SeedSpacing;
        return !SeedSprout.AnyNear(position, r, null) && !PixelPets.PlanterNear(position, r);
    }

    /// <summary>Plants a Seed sprout straight into the floor at 'position' (it grows a random pixel like any other). False when too many sprouts exist. Used by the Seed pet.</summary>
    public bool PlantSeedSproutAt(Vector3 position, int growTier = -1)
    {
        if (SeedSprout.Count >= seedSproutCap || !SeedSpotFree(position)) return false;
        GameObject go = new GameObject("Seed Sprout");
        go.transform.position = position;
        if (fallingCopyLayer >= 0 && fallingCopyLayer < 32) go.layer = fallingCopyLayer;
        SeedSprout sprout = go.AddComponent<SeedSprout>();
        sprout.Setup(this, OldPixelWorldSize, seedGrowSeconds, seedHoldSeconds, seedSproutScale);
        sprout.ForceTier(growTier);
        sprout.SetLifetime(seedSproutLifetime > 0f ? seedSproutLifetime : 45f);
        sprout.PlantNow();
        return true;
    }

    /// <summary>A cracked Seed shell: a small sprout pops out, falls, plants itself and grows a random old pixel (see <see cref="SeedSprout"/>).</summary>
    private void SpawnSeedSprout()
    {
        if (pixelTransform == null || SeedSprout.Count >= seedSproutCap) return;
        float unit = OldPixelWorldSize;

        GameObject go = new GameObject("Seed Sprout");
        go.transform.SetPositionAndRotation(pixelTransform.position, UnityEngine.Random.rotation);
        if (fallingCopyLayer >= 0 && fallingCopyLayer < 32) go.layer = fallingCopyLayer;

        BoxCollider box = go.AddComponent<BoxCollider>();
        box.size = Vector3.one * unit * 0.5f;
        box.center = new Vector3(0f, unit * 0.3f, 0f);
        ApplyHuskPhysics(box, null);   // (the shared husk values)
        // A sprout never collides with the live cube (it starts inside it), and, like a fresh old pixel, it is a ghost for a moment
        // (a trigger) so many sprouts born at the same spot don't jam against each other.
        foreach (Collider live in pixelTransform.GetComponentsInChildren<Collider>())
            Physics.IgnoreCollision(box, live);
        if (popNoCollisionSeconds > 0f) box.isTrigger = true;

        Rigidbody rb = go.AddComponent<Rigidbody>();
        rb.mass = Mathf.Max(0.0001f, fallingCopyMass);
        rb.useGravity = false;
        rb.collisionDetectionMode = collisionDetection;
#if UNITY_6000_0_OR_NEWER
        rb.linearDamping = fallingCopyDrag;
        rb.angularDamping = fallingCopyAngularDrag;
#else
        rb.drag = fallingCopyDrag;
        rb.angularDrag = fallingCopyAngularDrag;
#endif
        if (gravityScale > 0f) go.AddComponent<ScaledGravity>().scale = gravityScale;

        Vector2 flat = UnityEngine.Random.insideUnitCircle;
        if (flat.sqrMagnitude < 0.0001f) flat = Vector2.right;
        flat.Normalize();
        Vector3 direction = new Vector3(flat.x, UnityEngine.Random.Range(popVerticalRange.x, popVerticalRange.y), flat.y).normalized;
        rb.AddForce(direction * UnityEngine.Random.Range(popSpeedRange.x, popSpeedRange.y), ForceMode.VelocityChange);
        rb.AddTorque(UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(popSpinRange.x, popSpinRange.y), ForceMode.VelocityChange);

        SeedSprout sprout = go.AddComponent<SeedSprout>();
        float grow = seedlingGrowSeconds > 0f ? seedlingGrowSeconds : 40f;   // seedlings from a shell grow slowly
        sprout.Setup(this, unit, grow, seedHoldSeconds, seedSproutScale);
        sprout.SetGhost(popNoCollisionSeconds + 0.15f);
        sprout.SetLifetime((seedSproutLifetime > 0f ? seedSproutLifetime : 45f) + Mathf.Max(0f, grow - seedGrowSeconds));   // the despawn timer waits for the longer growth
        sprout.PlantsOnly();   // a cracked shell's seedlings grow plant pixels only
    }

    /// <summary>A right click on a message in a bottle: you let it drift away (no payout, no map) and the next pixel comes.</summary>
    private void DismissBottle()
    {
        PixelAudio.Play("fish_splash");
        PixelHints.Announce(bottleDriftText);
        hitsOnCurrentPixel = 0;
        if (randomizeSpawnTier) currentTierIndex = RollTier();
        Materialize(GetClickTier());
    }

    /// <summary>The whale tore itself off the hook (the fight ran out of time): no payout, the next pixel comes.</summary>
    public void WhaleGotAway()
    {
        PixelHints.Announce(whaleEscapedText);
        hitsOnCurrentPixel = 0;
        if (randomizeSpawnTier) currentTierIndex = RollTier();
        Materialize(GetClickTier());
    }

    /// <summary>
    /// Buried treasure: 'count' good fishing pixels (bait most often, then seaweed, shells, fish and pearls - whichever are unlocked) are paid at once
    /// and the same number of old pixels fountain out of the hole for show. Returns how many pixels it was.
    /// </summary>
    public int GiveFishingHaul(Vector3 origin, int count)
    {
        System.Collections.Generic.List<int> pool = new System.Collections.Generic.List<int>();
        System.Collections.Generic.List<float> weights = new System.Collections.Generic.List<float>();
        for (int i = 0; i < tiers.Length; i++)
        {
            PixelTier t = tiers[i];
            if (!t.unlocked || !IsFishBlock(t.type) || t.type == PixelType.Driftwood || t.type == PixelType.Bottle || t.type == PixelType.Whale) continue;
            pool.Add(i);
            weights.Add(t.type == PixelType.Bait ? 3f : t.type == PixelType.Pearl ? 0.8f : t.type == PixelType.Fish ? 1.2f : 2f);
        }
        if (pool.Count == 0 || count <= 0) return 0;
        float total = 0f;
        foreach (float w in weights) total += w;
        int[] picks = new int[count];
        double[] paid = new double[tiers.Length];
        for (int n = 0; n < count; n++)
        {
            float r = UnityEngine.Random.value * total;
            int k = 0;
            while (k < pool.Count - 1 && r > weights[k]) { r -= weights[k]; k++; }
            picks[n] = pool[k];
            paid[pool[k]] += RollOldPixelAmount(pool[k]) * 1.5d;
        }
        for (int t = 0; t < paid.Length; t++) if (paid[t] > 0d) AddCurrency(t, paid[t]);
        StartCoroutine(TreasureFountain(picks, origin));
        return count;
    }

    [Header("Treasure pixel")]
    [Tooltip("Turn off the Treasure chest's sparkles and sounds (it still works).")]
    [SerializeField] private bool disableTreasureFx = false;

    [Min(0f)]
    [Tooltip("How much the reward grows per click the chest took: payout = clicks x (1 + clicks x this). 0 = the coded default (0.15), so a 20-click chest pays about 80x a 1-click one.")]
    [SerializeField] private float treasureRewardGrowth = 0f;

    [Min(0)]
    [Tooltip("Pixels that pour out of an opened chest = this + the clicks it took (at most 'Treasure Max Pixels'). 0 = the coded default (2).")]
    [SerializeField] private int treasureBasePixels = 0;

    [Min(0)]
    [Tooltip("Most pixels one chest can pour out. 0 = the coded default (24).")]
    [SerializeField] private int treasureMaxPixels = 0;

    /// <summary>Reward multiplier of a chest that took 'need' clicks (grows faster than the clicks do).</summary>
    [Header("Electrician")]
    [Tooltip("The Capacitor pixel stores charge from every other click, up to this many (0 = 40) and pays 1 + charge x 'Capacitor Pay Per Charge' when collected.")]
    [SerializeField] private int capacitorChargeCap = 0;
    [Tooltip("Extra payout per stored charge for the Capacitor pixel (0 = 0.15).")]
    [SerializeField] private float capacitorPayPerCharge = 0f;
    [Tooltip("The Transistor pixel makes the NEXT pixel you collect pay this many times as much (0 = 2).")]
    [SerializeField] private float transistorMultiplier = 0f;
    [Tooltip("Seconds a Fuse pixel can be collected for its bonus before it burns out (0 = 3).")]
    [SerializeField] private float fuseSeconds = 0f;
    [Tooltip("Payout multiplier of a Fuse collected in time (0 = 3). Too late it pays 0.3.")]
    [SerializeField] private float fuseBonus = 0f;
    [Tooltip("Extra payout of a Bulb per old conductor pixel (Electric, Wire, Battery, Resistor) lying around, up to 30 of them (0 = 0.1).")]
    [SerializeField] private float bulbPerConductor = 0f;
    private float liveSince;

    /// <summary>The old pixels on the floor that carry current (Electric, Wire, Battery, Resistor), at most 30.</summary>
    public int CountConductors()
    {
        int n = 0;
        foreach (Rigidbody body in oldPixels)
        {
            if (body == null) continue;
            OldPixelInfo info = PixelHuskQuery.Get(body).info;
            if (info != null && IsValidTier(info.tierIndex) && IsConductorType(Tiers[info.tierIndex].type) && ++n >= 30) break;
        }
        return n;
    }

    /// <summary>Electric, Wire, Battery and Resistor pixels carry the Electrician's current.</summary>
    /// <summary>The Electrician pack's own pixels (Wire, Battery, Capacitor, Resistor, Transistor, Fuse, Bulb).</summary>
    public static bool IsElectricalBlock(PixelType t) => PixelBlockTags.Has(t, PixelTag.Electrical);   // (the tag table in PixelBlockTags.cs)

    public static bool IsConductorType(PixelType t) =>
        t == PixelType.Electric || t == PixelType.Wire || t == PixelType.Battery || t == PixelType.Resistor;

    /// <summary>How much charge the Capacitor pixel holds right now (every non-Capacitor click adds one).</summary>
    public static int CapacitorCharge => capacitorCharge;

    private float TreasureMultiplier(int need) => need * (1f + need * (treasureRewardGrowth > 0f ? treasureRewardGrowth : 0.15f));

    /// <summary>The chest cracked open: gold bursts out and a pile of other pixel types pours out (paid at once, the pixels that fly out are for show).</summary>
    private void OpenTreasure(int chestTier, int need)
    {
        Vector3 origin = pixelTransform.position;
        if (!disableTreasureFx) TreasureFx.Open(origin, PixelBaseSize, need);

        // Which pixel types can come out: any unlocked one except the chest itself, rare drops, fly-aways and wells.
        System.Collections.Generic.List<int> pool = new System.Collections.Generic.List<int>();
        for (int i = 0; i < tiers.Length; i++)
        {
            PixelTier t = tiers[i];
            if (i == chestTier || !t.unlocked || t.rareDrop || t.flyAway || t.type == PixelType.Treasure || t.type == PixelType.Singularity) continue;
            pool.Add(i);
        }
        int count = Mathf.Clamp((treasureBasePixels > 0 ? treasureBasePixels : 2) + need, 3, treasureMaxPixels > 0 ? treasureMaxPixels : 24);
        PixelStats.Count("treasure.opened");
        PixelStats.Best("treasure.most_clicks", need);
        if (pool.Count == 0) return;

        // Pay everything now; the old pixels that pour out carry nothing (so collecting them later doesn't pay twice).
        float bonus = 1f + need / 10f;
        int[] picks = new int[count];
        double[] paid = new double[tiers.Length];
        for (int i = 0; i < count; i++)
        {
            int t = pool[UnityEngine.Random.Range(0, pool.Count)];
            picks[i] = t;
            paid[t] += RollOldPixelAmount(t) * bonus;
        }
        for (int t = 0; t < paid.Length; t++) if (paid[t] > 0d) AddCurrency(t, paid[t]);
        PixelHints.Announce(need >= 15 ? "JACKPOT! The chest held " + count + " pixels" : "The treasure chest held " + count + " pixels");
        StartCoroutine(TreasureFountain(picks, origin));
    }

    private IEnumerator TreasureFountain(int[] picks, Vector3 origin)
    {
        float size = PixelBaseSize;
        for (int i = 0; i < picks.Length; i++)
        {
            float a = UnityEngine.Random.value * Mathf.PI * 2f;
            float speed = UnityEngine.Random.Range(1.2f, 3.2f) * size;
            Vector3 vel = new Vector3(Mathf.Cos(a) * speed, UnityEngine.Random.Range(3.5f, 6.5f) * size, Mathf.Sin(a) * speed);
            SpawnStoredPixel(picks[i], 0d, origin + Vector3.up * size * 0.3f, vel);
            yield return new WaitForSeconds(0.035f);
        }
    }

    [Header("Fishing")]
    [Tooltip("Turn off the bait pellets a Bait block drops (and their effect on fish).")]
    [SerializeField] private bool disableBait = false;

    [Min(0)]
    [Tooltip("Pellets one Bait block drops. 0 = the default (6).")]
    [SerializeField] private int baitPellets = 0;

    [Min(0f)]
    [Tooltip("How much each pellet lying around adds to a Fish block's spawn weight (0.35 = +35% each). 0 = the default (0.35).")]
    [SerializeField] private float baitPerPellet = 0f;

    [Min(0f)]
    [Tooltip("Seconds a pellet lies there before it rots away. 0 = the default (60).")]
    [SerializeField] private float baitLifetime = 0f;

    [Min(0)]
    [Tooltip("Most pellets that can lie around at once (the oldest go first). 0 = the default (24).")]
    [SerializeField] private int baitMaxPellets = 0;

    [Min(0f)]
    [Tooltip("How many times wider than a normal hazard blast a going-off Jellyfish stings (and it also zaps every bait pellet). 0 = the default (1.5).")]
    [SerializeField] private float jellyfishBlastFactor = 0f;

    [Header("Texts (skill blocks)")]
    [Tooltip("Event log line the first time a weevil eats your sprouts.")] [SerializeField] private string weevilText = "A weevil is eating your sprouts! Click it!";
    [Tooltip("Event log line when you let a message bottle drift away.")] [SerializeField] private string bottleDriftText = "You let the bottle drift away";
    [Tooltip("Event log line when the whale breaks the fishing line.")] [SerializeField] private string whaleEscapedText = "The whale broke the line and swam away!";

    public bool DisableBait => disableBait;
    public int BaitPellets => baitPellets > 0 ? baitPellets : 6;
    public float BaitPerPellet => baitPerPellet > 0f ? baitPerPellet : 0.35f;
    public float BaitLifetime => baitLifetime > 0f ? baitLifetime : 60f;
    public int BaitMaxPellets => baitMaxPellets > 0 ? baitMaxPellets : 24;

    private bool fishingBusy, fishingPass;
    private int fishNeed, fishReel;   // reeling a fish in the long way (the Weak Spots minigame is switched off)
}
