// ---- System notes ----
// PixelClickerHusks.cs
//   Part of partial class PixelClicker: everything about HUSKS (the pixels that drop off the cube; code calls them old pixels) - spawning
//   the falling copy after a click (SpawnFallingCopy / SpawnFallingCopyInner, SpawnStoredPixel, SpawnStreamedPixel), how long they last
//   (LifetimeFactor, AddDespawn), their amount (RollOldPixelAmount), the cap (CopyCap), the Vacuum pixel sucking them up (Vacuum,
//   SuckRoutine), husk XP, saving / restoring them (OldPixelState), claiming / absorbing / releasing them for devices, their physics
//   materials, trails and fly-away landing, and display copies (CreateDisplayPixel). The components ON a husk are in PixelHusks.cs.
//   Split out of PixelClicker.cs (no behaviour change).
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
    /// <summary>Frame time for the cube's click animations: they keep running while Time Stop has frozen the game clock.</summary>
    private int CopyCap => DevNoOldPixelLimit ? 0 : PixelTimeStop.IsStopped ? (timeStopStockpileMax <= 0 ? 0 : Mathf.Max(timeStopStockpileMax, OldPixelCap)) : OldPixelCap;

    [Tooltip("Old Solar pixels last this many times as long as other old pixels, so it is easier to gather a group of them. 0 = 1.6.")]
    [SerializeField] private float solarLifetimeFactor = 0f;

    private float LifetimeFactor(int tierIndex)
    {
        if (!IsValidTier(tierIndex)) return 1f;
        float factor = tiers[tierIndex].type == PixelType.Solar ? (solarLifetimeFactor > 0f ? solarLifetimeFactor : 1.6f) : 1f;
        return factor * PixelSkills.SkillLifetimeFactor(tiers[tierIndex].type);   // + a skill perk (Woodcutting level 3: wood lasts longer)
    }

    /// <summary>The edge length of an old pixel in world units.</summary>
    public float OldPixelWorldSize => PixelBaseSize * oldPixelScale;

    /// <summary>What one harvest of this pixel type would pay now (a random payout rolled for random-payout types, with the Value / Ultra multipliers).</summary>
    public double RollOldPixelAmount(int tierIndex)
    {
        if (!IsValidTier(tierIndex)) return 0d;
        PixelTier t = tiers[tierIndex];
        double basePayout = t.amountPerClick;
        if (t.randomPayout) basePayout = UnityEngine.Random.Range(Mathf.Min(t.payoutMin, t.payoutMax), Mathf.Max(t.payoutMin, t.payoutMax) + 1);
        return basePayout * PayoutMultiplier(tierIndex) * clickMultiplier;
    }

    /// <summary>
    /// Vacuum pixel effect: every old pixel flies into the cube (pixel rules: husk XP; with husksPayCurrency its original reward is added again).
    /// </summary>
    // The tier of each pixel the last vacuum click sucked up, waiting for the old Vacuum pixel that drops in the same click.
    private readonly System.Collections.Generic.List<int> vacuumContents = new System.Collections.Generic.List<int>();

    /// <summary>
    /// Vacuum effect. By default every old pixel swirls into the cube; a 'target' (e.g. a Vacuum pixel spat out of the Pixel Bank hose)
    /// makes them swirl into that instead, and 'exclude' keeps that pixel itself out of the suction.
    /// </summary>
    public void Vacuum(int vacuumTierIndex, Transform target = null, Rigidbody exclude = null)
    {
        double total = 0d;
        int count = 0;
        double[] perTier = new double[tiers.Length];
        int[] huskCounts = new int[tiers.Length];   // pixel rules: what was sucked up, per type, for the XP it gives
        vacuumContents.Clear();

        for (int i = 0; i < oldPixels.Count; i++)
        {
            Rigidbody body = oldPixels[i];
            if (body == null || body == exclude) continue;

            OldPixelInfo info = body.GetComponent<OldPixelInfo>();
            if (info != null && IsValidTier(info.tierIndex))
            {
                if (husksPayCurrency)
                {
                    AddCurrency(info.tierIndex, info.amount);
                    perTier[info.tierIndex] += info.amount;
                    total += info.amount;
                }
                else huskCounts[info.tierIndex]++;
                PixelSkills.OldPixelConsumed(info.tierIndex);
                count++;
                if (!hideVacuumContents) vacuumContents.Add(info.tierIndex);
            }

            StartCoroutine(SuckRoutine(body, target));
        }

        oldPixels.Clear();
        if (exclude != null) oldPixels.Add(exclude); // the pixel that is doing the sucking stays an old pixel

        if (logVacuum && count > 0)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder("Vacuum: re-collected " + count + " old pixels worth " + total + " (");
            for (int i = 0; i < tiers.Length; i++)
                if (perTier[i] > 0d) sb.Append(tiers[i].displayName).Append(" +").Append(perTier[i]).Append("  ");
            Debug.Log(sb.Append(")").ToString(), this);
        }

        for (int i = 0; i < huskCounts.Length; i++)
            if (huskCounts[i] > 0) GiveHuskXp(i, huskCounts[i]);

        if (count > 0)
        {
            if (total > 0d) VacuumBreakdown?.Invoke(perTier);
            PixelsVacuumed?.Invoke(vacuumTierIndex, total, count);   // total is 0 under the pixel rules (the sound and stats still use the count)
        }
    }

    /// <summary>Pixel rules: true = old pixels pay their value again when consumed (the old behaviour); false = they are husks (material, XP).</summary>
    public bool HusksPayCurrency => husksPayCurrency;

    /// <summary>Pixel rules: the XP 'count' consumed husks of one type give (a share of a manual click's XP; pure husks train Standard, a skill's husks that skill).</summary>
    public void GiveHuskXp(int tierIndex, int count)
    {
        if (husksPayCurrency || count <= 0 || !IsValidTier(tierIndex)) return;
        PixelSkills skills = PixelSkills.Instance;
        if (skills != null) skills.AddHuskXp(tierIndex, count * (huskXpShare > 0f ? huskXpShare : 0.5f));
    }

    public void ClearOldPixels()
    {
        for (int i = 0; i < oldPixels.Count; i++)
            if (oldPixels[i] != null) DespawnOldPixel(oldPixels[i].gameObject);
        oldPixels.Clear();
    }

    /// <summary>One old pixel as stored in a save file.</summary>
    [System.Serializable]
    public class OldPixelState
    {
        public int tier;
        public double amount;
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;
        public Vector3 velocity;
        public Vector3 angularVelocity;
        public float remaining = -1f;
    }

    /// <summary>The old pixels lying around, for the save file (not the ones streaking away or already vanishing).</summary>
    public System.Collections.Generic.List<OldPixelState> GetOldPixelStates()
    {
        var list = new System.Collections.Generic.List<OldPixelState>();
        for (int i = 0; i < oldPixels.Count; i++)
        {
            Rigidbody rb = oldPixels[i];
            if (rb == null || IsFlyingPixel(rb) || PixelConveyorSystem.IsRiding(rb)) continue;   // (pixels riding a belt are saved with the belt)
            OldPixelInfo info = rb.GetComponent<OldPixelInfo>();
            if (info == null) continue;
            OldPixelDespawn d = rb.GetComponent<OldPixelDespawn>();
            if (d != null && d.IsDespawning) continue;
            list.Add(new OldPixelState
            {
                tier = info.tierIndex,
                amount = info.amount,
                position = rb.position,
                rotation = rb.rotation,
#if UNITY_6000_0_OR_NEWER
                velocity = rb.isKinematic ? Vector3.zero : rb.linearVelocity,
#else
                velocity = rb.isKinematic ? Vector3.zero : rb.velocity,
#endif
                angularVelocity = rb.isKinematic ? Vector3.zero : rb.angularVelocity,
                remaining = d != null ? d.Remaining : -1f,
            });
        }
        return list;
    }

    /// <summary>Loading a save: replaces the old pixels lying around with the saved ones, where they were and how fast they moved.</summary>
    public void RestoreOldPixels(System.Collections.Generic.IEnumerable<OldPixelState> states)
    {
        ClearOldPixels();
        if (states == null) return;
        foreach (OldPixelState s in states)
        {
            if (s == null || !IsValidTier(s.tier) || tiers[s.tier].flyAway) continue;
            int before = oldPixels.Count;
            SpawnStoredPixel(s.tier, s.amount, s.position, s.velocity);
            if (oldPixels.Count <= before) continue;
            Rigidbody rb = oldPixels[oldPixels.Count - 1];
            if (rb == null) continue;
            rb.position = s.position;
            rb.rotation = s.rotation;
            rb.transform.SetPositionAndRotation(s.position, s.rotation);
            // No pop-in for loaded pixels: full size and solid straight away (the tiny / ghost phase made them fall through each other).
            OldPixelPopIn pop = rb.GetComponent<OldPixelPopIn>();
            if (pop != null) pop.Finish();
            // Pixels that were lying still stay frozen where they were; ones that were moving carry on.
            bool resting = s.velocity.sqrMagnitude < 0.25f * 0.25f;
            Vector3 vel = resting ? Vector3.zero : s.velocity;
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = vel;
#else
            rb.velocity = vel;
#endif
            rb.angularVelocity = resting ? Vector3.zero : s.angularVelocity;
            if (resting) rb.Sleep();
            OldPixelDespawn d = rb.GetComponent<OldPixelDespawn>();
            if (d != null) d.SetRemaining(s.remaining);
        }
    }

    /// <summary>The old pixels currently lying around (read-only). Used by the vacuum device.</summary>
    public System.Collections.Generic.IReadOnlyList<Rigidbody> OldPixels => oldPixels;

    /// <summary>Takes an old pixel out of the old-pixel list WITHOUT paying anything: the caller (the Wood Chipper) now owns the object and must destroy it.</summary>
    public bool ClaimOldPixel(Rigidbody body)
    {
        if (body == null || !oldPixels.Remove(body)) return false;
        PixelStats.Count("old.shredded");
        return true;
    }

    /// <summary>
    /// Consumes one old pixel (pixel rules: it gives husk XP; with husksPayCurrency its reward is re-added) and pulls it into 'target' while it shrinks.
    /// Returns false if it isn't an old pixel any more.
    /// </summary>
    public bool AbsorbOldPixel(Rigidbody body, Transform target)
    {
        if (body == null || !oldPixels.Remove(body)) return false;

        PixelStats.Count("old.vacuumed");
        OldPixelInfo info = body.GetComponent<OldPixelInfo>();
        if (info != null && IsValidTier(info.tierIndex))
        {
            PixelSkills.OldPixelConsumed(info.tierIndex);
            if (husksPayCurrency)
            {
                AddCurrency(info.tierIndex, info.amount);
                double[] perTier = new double[tiers.Length];
                perTier[info.tierIndex] = info.amount;
                VacuumBreakdown?.Invoke(perTier);
            }
            else GiveHuskXp(info.tierIndex, 1);
        }

        StartCoroutine(SuckRoutine(body, target));
        return true;
    }

    /// <summary>Hides / shows every old pixel on the floor (renderers only; they keep existing). Used while the floor is faded out on the main menu.</summary>
    public void CollectOldPixelTransforms(List<Transform> into)
    {
        foreach (Rigidbody rb in oldPixels) if (rb != null) into.Add(rb.transform);
    }

    public void SetOldPixelsHidden(bool hidden)
    {
        foreach (Rigidbody rb in oldPixels)
        {
            if (rb == null) continue;
            foreach (Renderer r in rb.GetComponentsInChildren<Renderer>(true)) r.forceRenderingOff = hidden;
        }
    }

    /// <summary>Stops and clears every particle system on an old pixel (its look's ambient effects) - used when something takes the pixel away.</summary>
    private static void QuietParticles(GameObject go)
    {
        if (go == null) return;
        foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>(true))
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    /// <summary>
    /// Takes an old pixel out of the game's old-pixel list so something else (the black hole) can handle it.
    /// 'credit' re-adds its original reward, but only with husksPayCurrency on (pixel rules). The caller is responsible for removing the object. Returns false if it wasn't listed.
    /// </summary>
    public bool ReleaseOldPixel(Rigidbody body, bool credit)
    {
        if (body == null || !oldPixels.Remove(body)) return false;
        QuietParticles(body.gameObject);   // a pixel being taken away stops emitting (the Vacuum's suction wind, embers, motes...)

        if (credit && husksPayCurrency)   // pixel rules: a husk never pays its value again
        {
            OldPixelInfo info = body.GetComponent<OldPixelInfo>();
            if (info != null && IsValidTier(info.tierIndex)) AddCurrency(info.tierIndex, info.amount);
        }
        return true;
    }

    /// <summary>Puts a husk that was taken off the list (ReleaseOldPixel) back on it as a normal old pixel (a husk picked back out of a Recycler).</summary>
    public void AdoptOldPixel(Rigidbody body)
    {
        if (body != null && !oldPixels.Contains(body)) oldPixels.Add(body);
    }

    /// <summary>The sound id of a pixel type's own bounce ("pixel_bounce_electric"), or null for an invalid tier. PixelAudio falls back to the plain landing sound if no such sound has clips.</summary>
    private string BounceSoundId(int tierIndex) =>
        IsValidTier(tierIndex) ? "pixel_bounce_" + tiers[tierIndex].type.ToString().ToLowerInvariant() : null;

#if UNITY_6000_0_OR_NEWER
    private PhysicsMaterial oldPixelPhysics;
#else
    private PhysicMaterial oldPixelPhysics;
#endif
    private float oldPixelPhysicsBounce = -1f, oldPixelPhysicsFriction = -1f;
    private readonly System.Collections.Generic.Dictionary<PixelType, UnityEngine.Object> lookPhysics =
        new System.Collections.Generic.Dictionary<PixelType, UnityEngine.Object>();
    private readonly System.Collections.Generic.Dictionary<int, Material> trailMaterials = new System.Collections.Generic.Dictionary<int, Material>();
    private readonly System.Collections.Generic.Dictionary<int, Color> trailMaterialColors = new System.Collections.Generic.Dictionary<int, Color>();

    /// <summary>One physics material shared by every old pixel (rebuilt when bounciness / friction change).</summary>
    /// <summary>Bounce / friction material of one pixel type's look (cached; rebuilt if the values change).</summary>
#if UNITY_6000_0_OR_NEWER
    private PhysicsMaterial LookPhysicsMaterial(PixelLook look)
    {
        if (lookPhysics.TryGetValue(look.type, out UnityEngine.Object cached) && cached != null)
        {
            PhysicsMaterial existing = (PhysicsMaterial)cached;
            if (Mathf.Approximately(existing.bounciness, look.bounce) && Mathf.Approximately(existing.dynamicFriction, look.friction)) return existing;
        }
        PhysicsMaterial m = new PhysicsMaterial("OldPixel_" + look.type)
        {
            bounciness = look.bounce, dynamicFriction = look.friction, staticFriction = look.friction,
            bounceCombine = PhysicsMaterialCombine.Maximum, frictionCombine = PhysicsMaterialCombine.Minimum
        };
        lookPhysics[look.type] = m;
        return m;
    }
#else
    private PhysicMaterial LookPhysicsMaterial(PixelLook look)
    {
        if (lookPhysics.TryGetValue(look.type, out UnityEngine.Object cached) && cached != null)
        {
            PhysicMaterial existing = (PhysicMaterial)cached;
            if (Mathf.Approximately(existing.bounciness, look.bounce) && Mathf.Approximately(existing.dynamicFriction, look.friction)) return existing;
        }
        PhysicMaterial m = new PhysicMaterial("OldPixel_" + look.type)
        {
            bounciness = look.bounce, dynamicFriction = look.friction, staticFriction = look.friction,
            bounceCombine = PhysicMaterialCombine.Maximum, frictionCombine = PhysicMaterialCombine.Minimum
        };
        lookPhysics[look.type] = m;
        return m;
    }
#endif

#if UNITY_6000_0_OR_NEWER
    private PhysicsMaterial SharedOldPixelPhysicsMaterial()
    {
        if (oldPixelPhysics == null || oldPixelPhysicsBounce != bounciness || oldPixelPhysicsFriction != friction)
        {
            oldPixelPhysics = new PhysicsMaterial("OldPixel")
            {
                bounciness = bounciness,
                dynamicFriction = friction,
                staticFriction = friction,
                bounceCombine = PhysicsMaterialCombine.Maximum,
                frictionCombine = PhysicsMaterialCombine.Average
            };
            oldPixelPhysicsBounce = bounciness;
            oldPixelPhysicsFriction = friction;
        }
        return oldPixelPhysics;
    }
#else
    private PhysicMaterial SharedOldPixelPhysicsMaterial()
    {
        if (oldPixelPhysics == null || oldPixelPhysicsBounce != bounciness || oldPixelPhysicsFriction != friction)
        {
            oldPixelPhysics = new PhysicMaterial("OldPixel")
            {
                bounciness = bounciness,
                dynamicFriction = friction,
                staticFriction = friction,
                bounceCombine = PhysicMaterialCombine.Maximum,
                frictionCombine = PhysicMaterialCombine.Average
            };
            oldPixelPhysicsBounce = bounciness;
            oldPixelPhysicsFriction = friction;
        }
        return oldPixelPhysics;
    }
#endif

    /// <summary>One trail material per pixel type, shared by all of its fly-away copies.</summary>
    private Material TrailMaterialFor(int tierIndex, Color tierColor)
    {
        if (trailMaterials.TryGetValue(tierIndex, out Material cached) && cached != null &&
            trailMaterialColors[tierIndex] == tierColor)
            return cached;

        Material mat = CreateVisualMaterial(new Color(tierColor.r, tierColor.g, tierColor.b, 0.6f), true);
        trailMaterials[tierIndex] = mat;
        trailMaterialColors[tierIndex] = tierColor;
        return mat;
    }

    /// <summary>
    /// Moves a falling copy's drawing onto a child object that wobbles (the pixel's own scale is used by pop-in / despawn,
    /// so the wobble can't share it).
    /// </summary>
    private static void MakeWobbleVisual(GameObject copy, PixelLook look)
    {
        MeshFilter mf = copy.GetComponent<MeshFilter>();
        MeshRenderer mr = copy.GetComponent<MeshRenderer>();
        if (mf == null || mr == null) return;

        GameObject visual = new GameObject("Wobble Visual", typeof(MeshFilter), typeof(MeshRenderer));
        visual.transform.SetParent(copy.transform, false);
        visual.layer = copy.layer;
        visual.GetComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
        MeshRenderer vr = visual.GetComponent<MeshRenderer>();
        vr.sharedMaterial = mr.sharedMaterial;
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        mr.GetPropertyBlock(block);
        vr.SetPropertyBlock(block);
        vr.shadowCastingMode = mr.shadowCastingMode;
        mr.enabled = false;
        visual.AddComponent<PixelLookWobble>().Setup(look.wobbleAmount, look.wobbleSpeed);
    }

    private bool IsBrightOldPixel(int tierIndex)
    {
        if (!IsValidTier(tierIndex) || brightOldPixelTypes == null) return false;
        PixelTier t = tiers[tierIndex];
        if (!t.glow) return false;
        for (int i = 0; i < brightOldPixelTypes.Length; i++)
            if (brightOldPixelTypes[i] == t.type) return true;
        return false;
    }

    private Material lightTrailMaterial;

    /// <summary>A glowing, fading trail behind a falling old pixel (colours come from the trail's gradient).</summary>
    private void AddLightTrail(GameObject copy, Color tierColor)
    {
        if (lightTrailMaterial == null)
        {
            // Sprites/Default uses the trail's vertex colours in every render pipeline; fall back to a tinted copy of the pixel material.
            Shader shader = PixelShaders.SpriteDefault();
            lightTrailMaterial = shader != null ? new Material(shader) { name = "LightTrail" }
                                                : CreateVisualMaterial(Color.white, true);
        }

        Color hot = Color.Lerp(tierColor, Color.white, Mathf.Clamp01((lightTrailBrightness - 1f) * 0.5f));
        hot = new Color(Mathf.Clamp01(hot.r * Mathf.Min(lightTrailBrightness, 1.5f)), Mathf.Clamp01(hot.g * Mathf.Min(lightTrailBrightness, 1.5f)),
                        Mathf.Clamp01(hot.b * Mathf.Min(lightTrailBrightness, 1.5f)), 1f);

        TrailRenderer trail = copy.AddComponent<TrailRenderer>();
        trail.sharedMaterial = lightTrailMaterial;
        trail.time = lightTrailSeconds;
        trail.minVertexDistance = 0.04f;
        trail.alignment = LineAlignment.View;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.widthMultiplier = copy.transform.lossyScale.x * lightTrailWidth;
        trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));

        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(hot, 0f), new GradientColorKey(tierColor, 1f) },
            new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = g;
    }

    /// <summary>Pulls one old pixel into the cube while shrinking it, then removes it.</summary>
    private IEnumerator SuckRoutine(Rigidbody body, Transform target = null)
    {
        Transform dest = target != null ? target : pixelTransform;
        Transform t = body.transform;
        foreach (Collider c in body.GetComponents<Collider>()) c.enabled = false;
        body.isKinematic = true;
        QuietParticles(body.gameObject);

        Vector3 startPos = t.position;
        Vector3 endPos = dest != null ? dest.position : startPos;
        Vector3 startScale = t.localScale;
        float time = 0f;

        // The swirl: the pixel's offset from the target turns round the vertical axis while it shrinks towards the centre.
        Vector3 startOffset = startPos - endPos;
        Vector2 startFlat = new Vector2(startOffset.x, startOffset.z);
        float startRadius = startFlat.magnitude;
        float startAngle = Mathf.Atan2(startOffset.z, startOffset.x);
        // Far pixels curve the most, nearby ones barely curve, so a big cleanup is a gentle sweep rather than a washing machine.
        float turns = vacuumSwirlTurns > 0f ? vacuumSwirlTurns : 0.6f;
        turns *= Mathf.Clamp01(startRadius / (PixelBaseSize * 8f) + 0.2f);
        float spinDirection = UnityEngine.Random.value < 0.5f ? -1f : 1f; // some go round the other way for a busier look
        bool swirl = !disableVacuumSwirl && startRadius > 0.05f;

        while (time < vacuumSuckDuration)
        {
            if (t == null) yield break; // destroyed (lifetime ran out) while flying
            time += Time.deltaTime;
            float k = vacuumSuckDuration > 0f ? Mathf.Clamp01(time / vacuumSuckDuration) : 1f;
            float eased = vacuumSuckCurve.Evaluate(k);

            if (dest != null) endPos = dest.position;
            if (swirl)
            {
                float radius = startRadius * (1f - eased);
                float angle = startAngle + spinDirection * turns * Mathf.PI * 2f * eased;
                t.position = new Vector3(endPos.x + Mathf.Cos(angle) * radius,
                                         Mathf.Lerp(startPos.y, endPos.y, eased),
                                         endPos.z + Mathf.Sin(angle) * radius);
                t.Rotate(0f, spinDirection * 240f * Time.deltaTime, 0f, Space.World); // a lazy tumble as it goes round
            }
            else t.position = Vector3.Lerp(startPos, endPos, eased);
            t.localScale = startScale * (1f - eased);
            yield return null;
        }

        if (t != null) Destroy(t.gameObject);
    }

    /// <summary>Clones the visible pixel, adds real physics, and pops it out in a random direction.</summary>
    /// <summary>Gives an old pixel its lifetime: after 'lifetime' seconds (0 = never) it swells, shrinks away and is destroyed.</summary>
    private void AddDespawn(GameObject copy, float lifetime)
    {
        OldPixelDespawn d = copy.AddComponent<OldPixelDespawn>();
        d.Setup(lifetime, despawnSwellScale, despawnSwellSeconds, despawnShrinkSeconds);
    }

    /// <summary>Makes an old pixel swell and shrink away now (instead of just vanishing).</summary>
    private void DespawnOldPixel(GameObject copy)
    {
        OldPixelDespawn d = copy.GetComponent<OldPixelDespawn>();
        if (d == null)
        {
            AddDespawn(copy, 0f);
            d = copy.GetComponent<OldPixelDespawn>();
        }
        d.Begin();
    }

    /// <summary>
    /// Puts an old pixel of any type into the world at 'position' moving at 'velocity' (the pixel bank spitting one out).
    /// Fly-away types (meteor) can't be spawned this way.
    /// </summary>
    public bool SpawnStoredPixel(int tierIndex, double amount, Vector3 position, Vector3 velocity)
    {
        if (!IsValidTier(tierIndex) || tiers[tierIndex].flyAway || pixelTransform == null) return false;
        SpawnFallingCopy(tierIndex, amount, true, position, velocity);
        return true;
    }

    /// <summary>Like <see cref="SpawnStoredPixel"/> but the pixel is 'magnetized' (a Sorter's stream: no collisions while it flies, solid once it lands). Returns its body, or null.</summary>
    public Rigidbody SpawnStreamedPixel(int tierIndex, double amount, Vector3 position, Vector3 velocity)
    {
        if (!IsValidTier(tierIndex) || tiers[tierIndex].flyAway || pixelTransform == null) return null;
        SpawnFallingCopy(tierIndex, amount, true, position, velocity, true);
        return oldPixels.Count > 0 ? oldPixels[oldPixels.Count - 1] : null;
    }

    private static readonly ProfilerMarker spawnCopyMarker = new ProfilerMarker("Pixel.SpawnFallingCopy");
    private static readonly ProfilerMarker materializeMarker = new ProfilerMarker("Pixel.Materialize");
    private static MaterialPropertyBlock spawnBlock;
    private Collider[] liveColliders;

    private void SpawnFallingCopy(int tierIndex, double amount, bool stored = false, Vector3 storedPosition = default,
                                  Vector3 storedVelocity = default, bool streamed = false)
    {
        using (spawnCopyMarker.Auto()) SpawnFallingCopyInner(tierIndex, amount, stored, storedPosition, storedVelocity, streamed);
    }

    private void SpawnFallingCopyInner(int tierIndex, double amount, bool stored, Vector3 storedPosition, Vector3 storedVelocity, bool streamed)
    {
        // Skip if the pixel is mid-materialize and basically invisible.
        if (!stored && pixelTransform.localScale.sqrMagnitude < 0.0001f) return;

        // A working sorter spits the pixel out of its pipe instead of popping it out at random (not for fly-away pixels).
        bool routed = false;
        Vector3 routedPosition = pixelTransform.position, routedVelocity = Vector3.zero;
        bool flyType = tierIndex >= 0 && tierIndex < tiers.Length && tiers[tierIndex].flyAway;
        if (stored)
        {
            routed = true; // the pixel bank decides where it goes and how fast
            routedPosition = storedPosition;
            routedVelocity = storedVelocity;
        }
        else if (!flyType && PixelSorterDevice.Current != null)
            routed = PixelSorterDevice.Current.TryRoute(out routedPosition, out routedVelocity);

        GameObject copy = new GameObject("OldPixel");
        copy.transform.SetPositionAndRotation(routed ? routedPosition : pixelTransform.position,
                                              stored ? UnityEngine.Random.rotation : pixelTransform.rotation);
        Vector3 sourceScale = pixelTransform.lossyScale;
        if (stored)
        {
            sourceScale = baseScale; // the cube's normal size, whatever it is doing right now
            if (pixelTransform.parent != null) sourceScale = Vector3.Scale(sourceScale, pixelTransform.parent.lossyScale);
        }
        copy.transform.localScale = sourceScale * oldPixelScale;
        if (fallingCopyLayer >= 0 && fallingCopyLayer < 32) copy.layer = fallingCopyLayer;

        // Copy only the visuals (mesh + material) so we don't duplicate this script.
        MeshFilter srcFilter = pixelRenderer != null ? pixelRenderer.GetComponent<MeshFilter>() : null;
        Mesh copyMesh = MeshFor(IsValidTier(tierIndex) ? LookOf(tiers[tierIndex]) : null);
        if (srcFilter != null && pixelRenderer != null)
        {
            copy.AddComponent<MeshFilter>().sharedMesh = copyMesh;
            MeshRenderer mr = copy.AddComponent<MeshRenderer>();
            if (spawnBlock == null) spawnBlock = new MaterialPropertyBlock();
            MaterialPropertyBlock block = spawnBlock;   // (SetPropertyBlock copies it, so one block is reused instead of allocating one per click)
            block.Clear();
            if (stored && IsValidTier(tierIndex))
            {
                // A stored pixel looks like its own type, not like whatever the cube currently shows.
                PixelTier look = tiers[tierIndex];
                PixelLook styled = LookOf(look);
                Color drawColor = RenderColor(look, styled);
                Material m = MaterialForTier(look);
                mr.sharedMaterial = m != null ? m : pixelRenderer.sharedMaterial;
                block.SetColor(colorPropertyId, drawColor);
                block.SetColor("_Color", drawColor);
                ApplyLookSurface(block, styled);
                ApplyLookTexture(block, styled, 0, look.clicksToCollect);
                float glowMul = styled != null ? styled.glowScale : 1f;
                block.SetColor("_EmissionColor", look.glow
                    ? new Color(look.color.r, look.color.g, look.color.b, 1f) * (look.glowIntensity * glowMul)
                    : styled != null && styled.emission > 0f
                        ? new Color(drawColor.r, drawColor.g, drawColor.b, 1f) * styled.emission : Color.black);
            }
            else
            {
                mr.sharedMaterial = pixelRenderer.sharedMaterial;
                pixelRenderer.GetPropertyBlock(block);
            }
            if (!flyType && IsBrightOldPixel(tierIndex))
            {
                // Extra bright falling pixel: stronger emission than the cube had.
                PixelTier look = tiers[tierIndex];
                Color baseGlow = new Color(look.color.r, look.color.g, look.color.b, 1f);
                block.SetColor("_EmissionColor", baseGlow * (Mathf.Max(look.glowIntensity, 1f) * oldPixelGlowBoost));
            }
            mr.SetPropertyBlock(block);
        }

        // Collider with a physics material so bounce/friction are tweakable.
        BoxCollider box = copy.AddComponent<BoxCollider>();
        PixelLook oldLook = IsValidTier(tierIndex) ? LookOf(tiers[tierIndex]) : null;
        bool custom = oldLook != null && oldLook.customPhysics;
        box.sharedMaterial = custom ? LookPhysicsMaterial(oldLook) : SharedOldPixelPhysicsMaterial();

        // Keep the old pixel from colliding with / bumping the live one if requested.
        if (!collideWithLivePixel)
        {
            if (liveColliders == null) liveColliders = pixelTransform.GetComponentsInChildren<Collider>();
            foreach (Collider live in liveColliders)
                if (live != null) Physics.IgnoreCollision(box, live);
        }

        Rigidbody rb = copy.AddComponent<Rigidbody>();
        rb.mass = Mathf.Max(0.0001f, fallingCopyMass);
        rb.useGravity = false; // gravity applied by ScaledGravity so gravityScale works
        rb.collisionDetectionMode = collisionDetection;
#if UNITY_6000_0_OR_NEWER
        rb.linearDamping = fallingCopyDrag;
        rb.angularDamping = fallingCopyAngularDrag;
#else
        rb.drag = fallingCopyDrag;
        rb.angularDrag = fallingCopyAngularDrag;
#endif
        bool fly = tierIndex >= 0 && tierIndex < tiers.Length && tiers[tierIndex].flyAway;
        if (fly)
        {
            // Meteor-style: no gravity, no collisions, one straight direction (camera-relative), with a trail.
            // The collider stays as a trigger so a slowed-down meteor can still be picked up (see LandFlyingPixel).
            box.isTrigger = true;
            copy.AddComponent<OldPixelFlight>();
            Camera fc = targetCamera != null ? targetCamera : Camera.main;
            Vector2 d = tiers[tierIndex].flyDirection;
            if (tiers[tierIndex].flyRandomDirection)
            {
                Vector2 range = tiers[tierIndex].flyAngleRange;
                float angle = UnityEngine.Random.Range(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y)) * Mathf.Deg2Rad;
                d = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)); // always on the upper half of the screen
            }
            if (d.sqrMagnitude < 0.0001f) d = Vector2.right;
            d.Normalize();
            Vector3 dir = fc != null ? fc.transform.right * d.x + fc.transform.up * d.y : new Vector3(d.x, d.y, 0f);
            dir = Quaternion.AngleAxis(UnityEngine.Random.Range(-flySpreadDegrees, flySpreadDegrees),
                                       fc != null ? fc.transform.forward : Vector3.forward) * dir;
#if UNITY_6000_0_OR_NEWER
            rb.linearDamping = 0f;
            rb.angularDamping = 0f;
#else
            rb.drag = 0f;
            rb.angularDrag = 0f;
#endif
            rb.AddForce(dir.normalized * tiers[tierIndex].flySpeed, ForceMode.VelocityChange);
            FlyAwayLaunched?.Invoke(copy.transform.position);
            rb.AddTorque(UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(popSpinRange.x, popSpinRange.y),
                         ForceMode.VelocityChange);

            PixelLook flyLook = LookOf(tiers[tierIndex]);
            if (flyLook != null && flyLook.cometTail && !disableMeteorFx) PixelCometFx.AttachWake(copy.transform, copy.transform.lossyScale.x);

            if (flyTrailSeconds > 0f)
            {
                TrailRenderer trail = copy.AddComponent<TrailRenderer>();
                trail.time = flyTrailSeconds;
                trail.startWidth = copy.transform.lossyScale.x;
                trail.endWidth = 0f;
                Color tc = tiers[tierIndex].color;
                trail.sharedMaterial = TrailMaterialFor(tierIndex, tc);
                // Smooth: a gently curved taper, an eased fade, rounded ends, and many short segments.
                trail.alignment = LineAlignment.View;
                trail.minVertexDistance = 0.02f;
                trail.numCapVertices = 6;
                trail.numCornerVertices = 4;
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f, 0f, -0.6f), new Keyframe(0.35f, 0.62f), new Keyframe(1f, 0f, -1.2f, 0f));
                Gradient fade = new Gradient();
                fade.SetKeys(
                    new[] { new GradientColorKey(Color.Lerp(tc, Color.white, 0.25f), 0f), new GradientColorKey(tc, 0.4f), new GradientColorKey(tc, 1f) },
                    new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0.5f, 0.35f), new GradientAlphaKey(0.18f, 0.7f), new GradientAlphaKey(0f, 1f) });
                trail.colorGradient = fade;
            }
        }
        else if (gravityScale > 0f)
            copy.AddComponent<ScaledGravity>().scale = gravityScale * (custom ? oldLook.gravityMultiplier : 1f);

        if (!fly) copy.AddComponent<OldPixelImpact>().Setup(landMinSpeed, landFullVolumeSpeed, landCooldown, BounceSoundId(tierIndex));
        if (!fly && IsValidTier(tierIndex) && srcFilter != null)
        {
            // Look extras: neon outline / dark-matter core, and shattering for glass.
            PixelLook styled = LookOf(tiers[tierIndex]);
            if (styled != null)
            {
                PixelLooks.AddExtras(copy.transform, copyMesh, styled, tiers[tierIndex].color, defaultMaterial);
                if (styled.wobble) MakeWobbleVisual(copy, styled);
                if (tiers[tierIndex].vacuum && !stored && vacuumContents.Count > 0)
                {
                    // The old Vacuum pixel carries what it just sucked up, as tiny pixels inside it.
                    int maxShown = vacuumContentsMax > 0 ? vacuumContentsMax : 16;
                    while (vacuumContents.Count > maxShown) vacuumContents.RemoveAt(UnityEngine.Random.Range(0, vacuumContents.Count));
                    copy.AddComponent<PixelVacuumContents>().Fill(this, vacuumContents, vacuumContentSize > 0f ? vacuumContentSize : 0.26f);
                    vacuumContents.Clear();
                }
                if (styled.gravityWell)
                    copy.AddComponent<OldPixelGravityWell>().Setup(this, styled);
                if (styled.floatAway)
                    copy.AddComponent<OldPixelFloat>().Setup(this, styled.floatAfterBounces, styled.floatLift, styled.floatDriftSpeed);
                if (styled.splash)
                    copy.AddComponent<OldPixelSplash>().Setup(this);
                if (styled.sunFx && !IsGodCube(styled.type))   // (Ra shows the sun's corona but does not hover or pay a trickle)
                {
                    copy.AddComponent<OldPixelSolarHover>().Setup(this);
                    copy.AddComponent<OldPixelSolarAura>();
                }
                if (styled.radiation && !disableNuclearFx)
                    copy.AddComponent<OldPixelRadiation>().Setup(this, nuclearRadiusPixels > 0f ? nuclearRadiusPixels : 4f, nuclearAgeRate > 0f ? nuclearAgeRate : 40f);
                if (styled.burn && !disableFireFx)
                    copy.AddComponent<OldPixelBurn>().Setup(this, fireBurnSeconds > 0f ? fireBurnSeconds : 6f);
                if (styled.shatter)
                    copy.AddComponent<OldPixelShatter>().Setup(this, shatterMinSpeed, shardCount, shardSpeed, shardLifeSeconds, shardSize, shatterSoundId);
            }
        }
        if (!fly && lightTrail && IsBrightOldPixel(tierIndex)) AddLightTrail(copy, tiers[tierIndex].color);
        if (srcFilter != null && IsValidTier(tierIndex))
        {
            PixelTier glowTier = tiers[tierIndex];
            PixelLook glowLook = LookOf(glowTier);
            bool bright = IsBrightOldPixel(tierIndex); // bright old pixels (Luminescent) glow much harder, like their boosted emission
            float hdr = GlowShellHdr(glowTier) * (bright ? Mathf.Max(1f, oldPixelGlowBoost * 0.7f) : 1f);
            if (WantsGlowShell(glowTier, glowLook))
                PixelLooks.AddGlowShell(copy.transform, copyMesh, GlowShellColor(glowTier), GlowShellAlpha(glowTier, bright ? 1.3f : 1f), hdr: hdr);
            if (WantsGlowHalo(glowTier, glowLook))
                PixelLooks.AddGlowHalo(copy.transform, GlowShellColor(glowTier), GlowShellAlpha(glowTier, bright ? 1.3f : 1f), hdr, bright ? 2.4f : 1f);
        }

        if (fly)
        {
            AddDespawn(copy, flyLifetime);
            OldPixelInfo flyInfo = copy.AddComponent<OldPixelInfo>();
        flyInfo.bornTime = Time.unscaledTime;
            flyInfo.tierIndex = tierIndex;
            flyInfo.amount = amount;
            oldPixels.Add(rb);
            return;
        }

        if (routed)
        {
            rb.AddForce(routedVelocity, ForceMode.VelocityChange);
            rb.AddTorque(UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(popSpinRange.x, popSpinRange.y),
                         ForceMode.VelocityChange);
            AddDespawn(copy, FallingCopyLifetime * LifetimeFactor(tierIndex));
            OldPixelInfo routedInfo = copy.AddComponent<OldPixelInfo>();
        routedInfo.bornTime = Time.unscaledTime;
            routedInfo.tierIndex = tierIndex;
            routedInfo.amount = amount;
            if (!stored || streamed)
            {
                // A Sorter's stream: no collisions while the pixel is in the air, solid again once it has landed (see OldPixelStream).
                OldPixelStream stream = copy.AddComponent<OldPixelStream>();
                stream.Setup(this, box);
            }
            oldPixels.Add(rb);
            while (CopyCap > 0 && oldPixels.Count > CopyCap)
            {
                Rigidbody oldest = oldPixels[0];
                oldPixels.RemoveAt(0);
                if (oldest != null) { DespawnOldPixel(oldest.gameObject); PixelStats.Count("old.capped"); }   // (removed by the floor cap: counted for the session recorder)
            }
            return;
        }

        // Random direction: random heading on the ground plane, random vertical component - or AIMED by where a manual click hit the cube (PixelBurstPop).
        Vector2 flat = UnityEngine.Random.insideUnitCircle;
        if (flat.sqrMagnitude < 0.0001f) flat = Vector2.right;
        flat.Normalize();
        if (aimThisCopy) flat = AimedDirection(flat);
        float vertical = Mathf.Min(0f, UnityEngine.Random.Range(popVerticalRange.x, popVerticalRange.y));   // never upward: the copy leaves through a side of the cube, not over its top
        // It appears at the side face it is thrown from (not at the centre, where it could show over the top face).
        Vector3 sidePosition = copy.transform.position + new Vector3(flat.x, 0f, flat.y) * ((popSideOffset > 0f ? popSideOffset : 0.45f) * PixelBaseSize);
        copy.transform.position = sidePosition;
        rb.position = sidePosition;
        Vector3 direction = new Vector3(flat.x, vertical, flat.y).normalized;
        float speed = UnityEngine.Random.Range(popSpeedRange.x, popSpeedRange.y);

        Vector3 launch = direction * speed;
        if (TryBurstVelocity(copy.transform.position, flat, out Vector3 burstVelocity)) launch = burstVelocity;   // a long fall: thrown out in a random direction so it lands round the cube
        rb.AddForce(launch, ForceMode.VelocityChange);
        rb.AddTorque(UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(popSpinRange.x, popSpinRange.y),
                     ForceMode.VelocityChange);

        if (popGrowSeconds > 0f || popNoCollisionSeconds > 0f)
        {
            // Spawns tiny and collision-free so fast clicking doesn't make a pile-up; grows and becomes solid while falling.
            OldPixelPopIn pop = copy.AddComponent<OldPixelPopIn>();
            pop.Setup(box, copy.transform.localScale, popStartScale, popGrowSeconds, popNoCollisionSeconds);
        }

        AddDespawn(copy, FallingCopyLifetime * LifetimeFactor(tierIndex));

        OldPixelInfo info = copy.AddComponent<OldPixelInfo>();
        info.bornTime = Time.unscaledTime;
        info.tierIndex = tierIndex;
        info.amount = amount;
        PixelMold.TryAttachGlow(copy, tiers[tierIndex].type);   // a Berry / Mushroom / Herb / Truffle is sometimes a glowing variant

        oldPixels.Add(rb);
        while (CopyCap > 0 && oldPixels.Count > CopyCap)
        {
            Rigidbody oldest = oldPixels[0];
            oldPixels.RemoveAt(0);
            if (oldest != null) { DespawnOldPixel(oldest.gameObject); PixelStats.Count("old.capped"); }
        }
    }

    /// <summary>
    /// A display-only model of a pixel type (no physics) drawn with its current look: material, colour, surface texture, glow
    /// and extras (dark-matter core, face circles, wobble...). 'size' = edge length in world units. Used by the Ultra Pad.
    /// </summary>
    public GameObject CreateDisplayPixel(int tierIndex, Transform parent, float size)
    {
        if (!IsValidTier(tierIndex) || pixelRenderer == null) return null;
        MeshFilter srcFilter = pixelRenderer.GetComponent<MeshFilter>();
        if (srcFilter == null) return null;

        PixelTier tier = tiers[tierIndex];
        PixelLook styled = LookOf(tier);
        GameObject go = new GameObject("Pixel Model");
        go.transform.SetParent(parent, false);
        go.transform.localScale = Vector3.one * size;
        if (fallingCopyLayer >= 0 && fallingCopyLayer < 32) go.layer = fallingCopyLayer;

        Mesh displayMesh = MeshFor(styled);
        go.AddComponent<MeshFilter>().sharedMesh = displayMesh;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        Material m = MaterialForTier(tier);
        mr.sharedMaterial = m != null ? m : pixelRenderer.sharedMaterial;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        Color drawColor = RenderColor(tier, styled);
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        block.SetColor(colorPropertyId, drawColor);
        block.SetColor("_Color", drawColor);
        ApplyLookSurface(block, styled);
        ApplyLookTexture(block, styled, 0, tier.clicksToCollect);
        float glowMul = styled != null ? styled.glowScale : 1f;
        block.SetColor("_EmissionColor", tier.glow
            ? new Color(tier.color.r, tier.color.g, tier.color.b, 1f) * (tier.glowIntensity * glowMul)
            : styled != null && styled.emission > 0f
                ? new Color(drawColor.r, drawColor.g, drawColor.b, 1f) * styled.emission : Color.black);
        mr.SetPropertyBlock(block);

        if (styled != null)
        {
            PixelLooks.AddExtras(go.transform, displayMesh, styled, tier.color, defaultMaterial);
            if (styled.wobble) MakeWobbleVisual(go, styled);
        }
        if (WantsGlowShell(tier, styled)) PixelLooks.AddGlowShell(go.transform, displayMesh, GlowShellColor(tier), GlowShellAlpha(tier), hdr: GlowShellHdr(tier));
        if (WantsGlowHalo(tier, styled)) PixelLooks.AddGlowHalo(go.transform, GlowShellColor(tier), GlowShellAlpha(tier), GlowShellHdr(tier));
        return go;
    }

    /// <summary>True while an old pixel is still streaking away in a straight line (a meteor pixel nobody has caught).</summary>
    public bool IsFlyingPixel(Rigidbody body)
    {
        if (body == null) return false;
        OldPixelFlight flight = body.GetComponent<OldPixelFlight>();
        return flight != null && flight.Flying;
    }

    /// <summary>
    /// A flying meteor pixel that was grabbed (Pixel Grabbing + Time Slow) becomes an ordinary old pixel: solid, affected by
    /// gravity, kept inside the view, and given a normal lifetime.
    /// </summary>
    public void LandFlyingPixel(Rigidbody body)
    {
        if (!IsFlyingPixel(body)) return;
        body.GetComponent<OldPixelFlight>().Flying = false;

        BoxCollider box = body.GetComponent<BoxCollider>();
        if (box != null)
        {
            box.isTrigger = false;
            box.sharedMaterial = SharedOldPixelPhysicsMaterial();
            if (!collideWithLivePixel && pixelTransform != null)
                foreach (Collider live in pixelTransform.GetComponentsInChildren<Collider>())
                    Physics.IgnoreCollision(box, live);
        }

        body.collisionDetectionMode = collisionDetection;
#if UNITY_6000_0_OR_NEWER
        body.linearDamping = fallingCopyDrag;
        body.angularDamping = fallingCopyAngularDrag;
#else
        body.drag = fallingCopyDrag;
        body.angularDrag = fallingCopyAngularDrag;
#endif
        if (gravityScale > 0f && body.GetComponent<ScaledGravity>() == null)
            body.gameObject.AddComponent<ScaledGravity>().scale = gravityScale;
        if (body.GetComponent<OldPixelImpact>() == null)
            body.gameObject.AddComponent<OldPixelImpact>().Setup(landMinSpeed, landFullVolumeSpeed, landCooldown,
                BounceSoundId(body.GetComponent<OldPixelInfo>() != null ? body.GetComponent<OldPixelInfo>().tierIndex : -1));

        OldPixelDespawn despawn = body.GetComponent<OldPixelDespawn>();
        if (despawn != null) despawn.AddLifetime(FallingCopyLifetime);
    }
}
