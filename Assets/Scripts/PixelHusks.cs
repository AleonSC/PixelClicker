// ---- System notes ----
// PixelHusks.cs
//   Small helper components that ride on HUSKS (the pixels that drop off the cube; code calls them old pixels), split out of PixelClicker.cs:
//   ScaledGravity (gravity scale per body; skips sleeping husks so heaps can sleep - see docs/systems/PixelRules.md 'Husks settle'), OldPixelImpact (landing sound / pulse), OldPixelPopIn (pop-in animation), OldPixelFlight
//   (fly-away marker), OldPixelDespawn (lifetime, swell-and-shrink despawn, Frozen / HoldAll for black holes and takeovers),
//   OldPixelInfo (what tier a husk is, its amount, held-by-player tag). PixelClicker adds them in SpawnFallingCopy.
//   ScaledGravity, OldPixelImpact, OldPixelPopIn and OldPixelDespawn have no Update / FixedUpdate of their own: they register with PixelHuskTicker
//   while enabled and it calls their Tick (one loop for every husk instead of one engine call per husk per helper).
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

/// <summary>
/// Tiny helper added at runtime to old pixels so their gravity can be scaled
/// (Unity's Rigidbody only has on/off gravity).
/// </summary>
public class ScaledGravity : MonoBehaviour, IHuskTick
{
    [HideInInspector] public float scale = 1f;
    private Rigidbody body;

    private void Awake() => body = GetComponent<Rigidbody>();
    private void OnEnable() => PixelHuskTicker.AddPhysics(this);
    private void OnDisable() => PixelHuskTicker.RemovePhysics(this);

    /// <summary>Once per physics step (PixelHuskTicker).</summary>
    public void Tick()
    {
        // A sleeping husk is left asleep: AddForce would wake it every step, so resting husks could never sleep and heaps kept fidgeting.
        // Anything that touches it (another husk, a grab, Magnet, Fan, suction, its support taken away) wakes it and gravity resumes.
        if (body != null && !body.isKinematic && !body.IsSleeping())
            body.AddForce(Physics.gravity * scale, ForceMode.Acceleration);
    }
}

/// <summary>
/// Reports when an old pixel hits a surface hard enough to be heard (PixelClicker.OldPixelLanded). Pixels bumping into
/// each other don't count - only the floor and other solid surfaces.
/// </summary>
public class OldPixelImpact : MonoBehaviour, IHuskTick
{
    private void OnEnable() => PixelHuskTicker.AddPhysics(this);
    private void OnDisable() => PixelHuskTicker.RemovePhysics(this);

    private float minSpeed = 1.5f, fullSpeed = 8f, cooldown = 0.12f, lastTime = -99f;
    private string soundId; // a sound just for this pixel type's bounce (e.g. "pixel_bounce_electric"); plain landing sound when it has none

    public void Setup(float min, float full, float gap, string ownSoundId = null)
    {
        soundId = ownSoundId;
        minSpeed = min;
        fullSpeed = Mathf.Max(0.1f, full);
        cooldown = gap;
    }

    private float lastPulseTime = -99f;
    private Rigidbody pulseBody;
    private float lastFallSpeed;

    /// <summary>
    /// Safety net for the landing pulse: a pixel that was falling fast and has just stopped (or bounced) gets its ripple even when no collision event reached
    /// OnCollisionEnter (a pixel dropped from high up can come down hard enough for the physics to resolve the landing without one). A short ray straight down finds the surface.
    /// Only runs while the landing pulse is on and only for pixels that were falling fast, so it costs nothing otherwise. Once per physics step (PixelHuskTicker).
    /// </summary>
    public void Tick()
    {
        if (!PixelFloor.PulseActive) { lastFallSpeed = 0f; return; }
        if (pulseBody == null) pulseBody = GetComponent<Rigidbody>();
        if (pulseBody == null || pulseBody.isKinematic) { lastFallSpeed = 0f; return; }
#if UNITY_6000_0_OR_NEWER
        float vy = pulseBody.linearVelocity.y;
#else
        float vy = pulseBody.velocity.y;
#endif
        float fell = lastFallSpeed;
        lastFallSpeed = Mathf.Max(0f, -vy);
        if (fell < 2.5f || vy > -fell * 0.5f) return;   // was not falling fast, or is still falling
        if (Time.time - lastPulseTime < 0.15f) return;   // the collision event already made one
        float reach = Mathf.Max(0.3f, fell * Time.fixedDeltaTime * 3f + transform.lossyScale.y);
        int count = Physics.RaycastNonAlloc(transform.position, Vector3.down, downHits, reach, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue; RaycastHit surface = default; bool found = false;
        for (int i = 0; i < count; i++)
        {
            RaycastHit h = downHits[i];
            if (h.collider == null || h.collider.transform.IsChildOf(transform) || h.distance >= best) continue;
            if (h.collider.GetComponentInParent<OldPixelInfo>() != null || h.collider.GetComponent<WaterSplashPiece>() != null) continue;
            best = h.distance; surface = h; found = true;
        }
        if (!found) return;
        lastPulseTime = Time.time;
        OldPixelInfo info = GetComponent<OldPixelInfo>();
        PixelFloorPulse.LandedAt(surface.point, surface.normal, transform, info != null ? info.tierIndex : -1, Mathf.Clamp01(fell / fullSpeed));
    }

    private static readonly RaycastHit[] downHits = new RaycastHit[16];   // (reused by Tick)

    private OldPixelInfo landInfo;

    private void OnCollisionEnter(Collision collision)
    {
        // Its first touch of anything (the floor, a device, a pile of husks): it has landed (Husk Crush's air crush bonus ends).
        if (landInfo == null) landInfo = GetComponent<OldPixelInfo>();
        if (landInfo != null && !landInfo.landed) landInfo.landed = true;

        if (collision.collider.GetComponentInParent<OldPixelInfo>() != null) return; // another old pixel
        if (collision.collider.GetComponent<WaterSplashPiece>() != null) return;      // a splash piece

        float speed = collision.relativeVelocity.magnitude;
        if (speed >= 0.6f && Time.time - lastPulseTime > 0.08f)
        {
            // The landing ripple (floor invisible + 'Landing pulse'): softer landings count too, it has its own short gap.
            lastPulseTime = Time.time;
            OldPixelInfo landedInfo = GetComponent<OldPixelInfo>();
            PixelFloorPulse.Landed(collision, transform, landedInfo != null ? landedInfo.tierIndex : -1, Mathf.Clamp01(speed / fullSpeed));
        }

        if (Time.time - lastTime < cooldown) return;
        if (speed < minSpeed) return;

        lastTime = Time.time;
        float intensity = Mathf.Clamp01(speed / fullSpeed);
        if (!string.IsNullOrEmpty(soundId) && PixelAudio.Has(soundId)) PixelAudio.PlayScaled(soundId, Mathf.Lerp(0.3f, 1f, intensity));
        else PixelClicker.RaiseOldPixelLanded(intensity);
    }
}

/// <summary>
/// Makes a new old pixel start tiny and non-colliding, then grow to full size and become solid again.
/// </summary>
public class OldPixelPopIn : MonoBehaviour, IHuskTick
{
    private void OnEnable() => PixelHuskTicker.AddFrame(this);
    private void OnDisable() => PixelHuskTicker.RemoveFrame(this);

    private Collider body;
    private Vector3 fullScale;
    private float startScale, growSeconds, ghostSeconds, age;

    public void Setup(Collider collider, Vector3 full, float start, float grow, float noCollision)
    {
        body = collider;
        fullScale = full;
        startScale = start;
        growSeconds = grow;
        ghostSeconds = noCollision;
        if (body != null && ghostSeconds > 0f) body.isTrigger = true; // a trigger doesn't collide (keeps ignore-collision settings)
        transform.localScale = fullScale * (growSeconds > 0f ? startScale : 1f);
    }

    /// <summary>Skips the pop-in: full size and solid right now (used for pixels restored from a save).</summary>
    public void Finish()
    {
        enabled = false;
        transform.localScale = fullScale;
        if (body != null) body.isTrigger = false;
        Destroy(this);
    }

    /// <summary>Once per frame (PixelHuskTicker).</summary>
    public void Tick()
    {
        age += Time.deltaTime;
        if (growSeconds > 0f)
        {
            float k = Mathf.Clamp01(age / growSeconds);
            transform.localScale = fullScale * Mathf.Lerp(startScale, 1f, k * (2f - k)); // ease out
        }
        if (body != null && body.isTrigger && age >= ghostSeconds) body.isTrigger = false;
        if (age >= growSeconds && age >= ghostSeconds) { enabled = false; Destroy(this); }   // (enabled = false: off the ticker at once, Destroy only happens at the end of the frame)
    }
}

/// <summary>
/// Ends an old pixel's life: after its lifetime (or when Begin is called) it swells slightly for a split second,
/// then shrinks very quickly to nothing and is destroyed.
/// </summary>
/// <summary>Marks a meteor-style old pixel that is still flying away in a straight line (cleared when it is caught).</summary>
public class OldPixelFlight : MonoBehaviour
{
    public bool Flying = true;
}

public class OldPixelDespawn : MonoBehaviour, IHuskTick
{
    private void OnEnable() => PixelHuskTicker.AddFrame(this);
    private void OnDisable() => PixelHuskTicker.RemoveFrame(this);

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() // keeps static state clean when Enter Play Mode skips the domain reload
    {
        Frozen = false;
        DevNoDespawn = false;
        HoldAll = false;
        rateOwner = null;
        PixelStatics.ResetOnRestart(OnSceneUnloaded);
    }

    // (Restart reloads the scene: a black hole / takeover round cut short must not leave every husk frozen or held in the new game)
    private static void OnSceneUnloaded() { Frozen = false; HoldAll = false; }

    private float lifetime, swellScale, swellSeconds, shrinkSeconds, age, phaseTime;
    private bool despawning, naturalEnd;
    private float ageRate = 1f, rateRefresh;
    private OldPixelInfo rateInfo;
    private static PixelClicker rateOwner;

    /// <summary>How fast this pixel ages: slower while the pet of its pixel type is out (see PixelPets.DespawnRate).</summary>
    private float CurrentAgeRate()
    {
        if (rateInfo == null) rateInfo = GetComponent<OldPixelInfo>();
        if (rateInfo == null) return 1f;
        if (rateOwner == null) rateOwner = PixelFind.First<PixelClicker>();
        if (rateOwner == null || !rateOwner.IsValidTierIndex(rateInfo.tierIndex)) return 1f;
        return PixelPets.DespawnRate(rateOwner.Tiers[rateInfo.tierIndex].type);
    }

    /// <summary>True while the player holds this pixel (its lifetime is frozen).</summary>
    public bool Held { get; set; }

    /// <summary>While true, no old pixel's lifetime counts down (a black hole is open: time dilation). Pixels already vanishing finish.</summary>
    public static bool Frozen { get; set; }

    /// <summary>While true no old pixel's lifetime counts down (the Snake minigame holds every pixel in place). Unlike Frozen it is owned by one minigame.</summary>
    public static bool HoldAll { get; set; }

    /// <summary>Dev tools: old pixels never expire on their own while true.</summary>
    public static bool DevNoDespawn { get; set; }

    /// <summary>Gives the pixel more time before it starts to vanish (no effect once it is vanishing or if it never expires).</summary>
    /// <summary>Ages the pixel by 'seconds' at once (radiation). Respects held pixels, black holes, takeover minigames and the dev switch; ignores gravity-well keep-alive.</summary>
    public void Hasten(float seconds)
    {
        if (despawning || Held || Frozen || DevNoDespawn || HoldAll || lifetime <= 0f) return;
        age += seconds;
        if (age >= lifetime) Begin();
    }

    public void AddLifetime(float seconds)
    {
        if (!despawning && lifetime > 0f) lifetime += seconds;
    }

    /// <summary>Gives back up to 'seconds' of the time it has already lived (never more than it has lived), so it fades later. Husk Crush's Clean Break.</summary>
    public void GiveBackTime(float seconds)
    {
        if (!despawning && lifetime > 0f && seconds > 0f) age = Mathf.Max(0f, age - seconds);
    }

    /// <summary>Replaces the lifetime (seconds until it starts to vanish; the time already lived still counts). Ignored once a saved remaining time was restored.</summary>
    public void SetLifetime(float seconds)
    {
        if (!despawning && !restoredRemaining && seconds > 0f) lifetime = seconds;
    }

    private bool restoredRemaining;

    /// <summary>How far through its lifespan it is (0..1; 0 when it never expires).</summary>
    public float AgeFraction => lifetime > 0f ? Mathf.Clamp01(age / lifetime) : 0f;

    /// <summary>Seconds left before it starts to vanish (-1 = it never expires).</summary>
    public float Remaining => lifetime > 0f ? Mathf.Max(0f, lifetime - age) : -1f;

    /// <summary>Real seconds left before it starts to vanish: the aged-seconds left divided by its ageing speed (a pet of its type slows the ageing). -1 = it never expires.</summary>
    public float RemainingReal => lifetime > 0f ? Mathf.Max(0f, lifetime - age) / Mathf.Max(0.05f, ageRate) : -1f;

    /// <summary>Its lifespan is standing still right now (held, a black hole open, a minigame holding everything, kept alive by a gravity well, the dev switch).</summary>
    public bool Paused => !despawning && lifetime > 0f && (Held || Frozen || DevNoDespawn || HoldAll || Time.time < keepUntil);

    /// <summary>Loading a save: the pixel has this many seconds left (-1 = never expires).</summary>
    public void SetRemaining(float seconds)
    {
        if (despawning) return;
        restoredRemaining = true;
        age = 0f;
        lifetime = seconds >= 0f ? Mathf.Max(0.5f, seconds) : 0f;
    }

    /// <summary>Stops the lifetime counting down for a moment (called every physics step by a gravity well that has it in range).</summary>
    public void KeepAlive()
    {
        keepUntil = Time.time + 0.25f;
    }

    private float keepUntil;

    /// <summary>Is it being kept alive right now (a complete Solar group, a gravity well...)? The solar panel uses it to tell a complete group from stray Solar pixels.</summary>
    public bool IsKept => Time.time < keepUntil;

    /// <summary>True once it has started swelling and shrinking away.</summary>
    public bool IsDespawning => despawning;
    private Vector3 baseScale;

    public void Setup(float life, float swell, float swellTime, float shrinkTime)
    {
        lifetime = life;
        swellScale = swell;
        swellSeconds = Mathf.Max(0.01f, swellTime);
        shrinkSeconds = Mathf.Max(0.01f, shrinkTime);
    }

    /// <summary>Starts the swell-and-shrink now. Safe to call more than once.</summary>
    public void Begin()
    {
        if (despawning) return;
        despawning = true;
        baseScale = transform.localScale;

        OldPixelPopIn pop = GetComponent<OldPixelPopIn>();
        if (pop != null) { pop.enabled = false; Destroy(pop); } // stop growing; we take over the scale

        Collider c = GetComponent<Collider>();
        if (c != null) c.isTrigger = true; // a vanishing pixel shouldn't shove others around
    }

    /// <summary>Once per frame (PixelHuskTicker).</summary>
    public void Tick()
    {
        if (!despawning)
        {
            if (Held || Frozen || DevNoDespawn || HoldAll || Time.time < keepUntil) return;
            rateRefresh -= Time.deltaTime;
            if (rateRefresh <= 0f) { rateRefresh = 0.5f; ageRate = CurrentAgeRate(); }
            age += Time.deltaTime * ageRate;
            if (lifetime > 0f && age >= lifetime) { naturalEnd = true; PixelStats.Count("old.despawned"); Begin(); }   // its own timer ran out (not the cap, a minigame or a device)
            return;
        }

        phaseTime += Time.deltaTime;
        if (phaseTime < swellSeconds)
        {
            float k = phaseTime / swellSeconds;
            transform.localScale = baseScale * Mathf.Lerp(1f, swellScale, k);
        }
        else
        {
            float k = (phaseTime - swellSeconds) / shrinkSeconds;
            if (k >= 1f) { if (naturalEnd) PixelAudio.Play("pixel_vanish"); enabled = false; Destroy(gameObject); return; }   // (enabled = false: no more ticks before the end-of-frame Destroy)   // a small high bloop, only when its own despawn timer ended it (cleared by the capacity limit or used up = silent)
            transform.localScale = baseScale * Mathf.Lerp(swellScale, 0f, k * k);
        }
    }
}

/// <summary>
/// Runtime tag on each old pixel: which tier it was and how much it paid out.
/// The Vacuum pixel uses it to pay that amount again.
/// </summary>
public class OldPixelInfo : MonoBehaviour
{
    [HideInInspector] public int tierIndex;
    [HideInInspector] public double amount;
    /// <summary>Unscaled time the old pixel appeared (Pixel Grabbing ignores it for a split second so it can't be grabbed through the cube).</summary>
    [HideInInspector] public float bornTime = -99f;

    /// <summary>The player is holding this pixel right now (Pixel Grabbing).</summary>
    [HideInInspector] public bool heldByPlayer;

    /// <summary>Unscaled time the player last let go of this pixel (-99 = never held).</summary>
    [HideInInspector] public float lastHeldTime = -99f;

    /// <summary>It has touched something since it dropped (set by OldPixelImpact's first collision). Husk Crush's AIR CRUSH bonus needs it false.</summary>
    [HideInInspector] public bool landed;

    /// <summary>Game time until which mold can't infect this pixel (it was just shaken clean).</summary>
    [HideInInspector] public float moldImmuneUntil;

    /// <summary>Is this pixel tagged 'recently held by the player': held right now, or let go within 'seconds' (PixelGrab.RecentlyHeldSeconds, editable in the Inspector)? Storage Crates capture tagged pixels that land on them.</summary>
    public bool RecentlyHeld(float seconds) => heldByPlayer || Time.unscaledTime - lastHeldTime <= seconds;
}
