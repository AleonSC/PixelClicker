// ---- System notes ----
// PixelClickerAnimation.cs
//   Part of PixelClicker (partial class): the cube on screen (AnimatePixel: bob, spin, pulse, squash, shrink; cube offset / shrink for minigames; needed clicks; Materialize; PixelBaseSize; click effects and sounds). The system notes are in PixelClicker.cs.
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
    private static float AnimDelta => (PixelTimeStop.IsStopped || PixelTimeStop.IsSlowed || PixelGameMode.TitleShowing) ? Time.unscaledDeltaTime : Time.deltaTime;

    /// <summary>0..1 pulse that tells a brand-new player to click the cube: only while the guide text shows and nothing has been collected yet.</summary>
    private float ClickHintPulse(float time)
    {
        if (disableClickHint || clickHintDone || !ClickHint) return 0f;
        foreach (PixelTier t in tiers)
            if (t.totalCollected > 0d) { clickHintDone = true; return 0f; }
        return 0.5f + 0.5f * Mathf.Sin(time * clickHintSpeed * Mathf.PI * 2f);
    }

    private void AnimatePixel()
    {
        float time = Time.time;

        Vector3 dashOffset = cubeOffsetActive ? cubeOffset : Vector3.zero;
        if (suspendInMidair && hoverBob)
        {
            pixelTransform.position = basePosition + dashOffset +
                Vector3.up * (Mathf.Sin(time * hoverSpeed * Mathf.PI * 2f) * hoverAmplitude);
        }
        else if (cubeOffsetActive || cubeOffsetWasActive)
        {
            pixelTransform.position = basePosition + dashOffset;
        }
        cubeOffsetWasActive = cubeOffsetActive;

        if (idleSpin != Vector3.zero && allowRotation && !HoldSpin)
            pixelTransform.Rotate(idleSpin * AnimDelta, Space.Self);

        if (hitbox != null) hitbox.SetPositionAndRotation(pixelTransform.position, pixelTransform.rotation);

        float pulse = 0f;
        if (pulseEnabled && allowPulsing)
        {
            float cycle = Mathf.Repeat(time * pulseSpeed, 1f);
            pulse = pulseCurve.Evaluate(cycle); // -1..1
        }

        // Squash briefly when a tough pixel is hit but not yet collected.
        float punch = 1f;
        if (hitPunchTimer > 0f)
        {
            hitPunchTimer -= AnimDelta;
            punch = 1f - hitPunchAmount * Mathf.Clamp01(hitPunchTimer / Mathf.Max(0.01f, hitPunchDuration));
        }

        float clickSwell = 1f;
        if (clickPulseTimer > 0f)
        {
            float length = clickPulseSeconds > 0f ? clickPulseSeconds : 0.12f;
            clickPulseTimer -= AnimDelta;
            clickSwell = 1f + (clickPulseAmount > 0f ? clickPulseAmount : 0.03f) * Mathf.Sin(Mathf.Clamp01(1f - clickPulseTimer / length) * Mathf.PI);
        }

        Vector3 liveBase = baseScale;
        if (activeLook != null && activeLook.wobble && allowPulsing)
            liveBase = Vector3.Scale(baseScale, PixelWobble.Scale(time, activeLook.wobbleAmount, activeLook.wobbleSpeed, 0f));
        cubeShrinkCurrent = Mathf.MoveTowards(cubeShrinkCurrent, cubeShrinkTarget, Time.unscaledDeltaTime / Mathf.Max(0.05f, cubeShrinkSeconds));
        float hintPulse = ClickHintPulse(time);
        digShrinkCurrent = Mathf.MoveTowards(digShrinkCurrent, digShrinkTarget, Time.unscaledDeltaTime * 3f);
        pixelTransform.localScale = liveBase * (materializeFactor * punch * clickSwell * (1f + pulse * pulseAmount) * (1f + hintPulse * clickHintAmount) * cubeShrinkCurrent * digShrinkCurrent);

        if (pulseEnabled && allowPulsing && pulseBrightness)
        {
            Color pulsed = currentColor * (1f + pulse * brightnessAmount);
            pulsed.a = currentColor.a; // brightness only, keep see-through tiers see-through
            ApplyPixelColor(pulsed, false);
        }
        else if (activeGlow > 0f)
        {
            ApplyPixelColor(currentColor, false); // keeps the glow breathing
        }

        if (hintPulse > 0f && clickHintBrightness > 0f)
        {
            Color bright = currentColor * (1f + hintPulse * clickHintBrightness);
            bright.a = currentColor.a;
            ApplyPixelColor(bright, false);
        }

        if (glowLight != null && glowLight.enabled)
        {
            float breath = GlowBreath(activeGlowType, time);
            glowLight.intensity = glowLightIntensity * activeGlow * breath;
        }

        AnimateGlowShell(time);
    }

    private Vector3 cubeOffset;
    private bool cubeOffsetActive, cubeOffsetWasActive;

    /// <summary>Moves the clickable cube by a world offset from its home spot (Cube Dash); null puts it back.</summary>
    public void SetCubeOffset(Vector3? offset)
    {
        cubeOffsetActive = offset.HasValue;
        cubeOffset = offset ?? Vector3.zero;
    }

    /// <summary>The cube's home position (without any minigame offset or bobbing).</summary>
    public Vector3 CubeBasePosition => basePosition;

    /// <summary>The tier index of the pixel on the cube right now.</summary>
    public int CurrentClickTierIndex => GetClickTierIndex();

    /// <summary>The pixel type on the cube right now.</summary>
    public PixelType LiveType => tiers != null && tiers.Length > 0 ? tiers[GetClickTierIndex()].type : PixelType.White;

    /// <summary>Hits already put into the pixel on the cube.</summary>
    public int LiveHits => hitsOnCurrentPixel;

    private float cubeShrinkCurrent = 1f, cubeShrinkTarget = 1f, cubeShrinkSeconds = 0.5f;
    private float digShrinkCurrent = 1f, digShrinkTarget = 1f;   // a Dirt block gets smaller with every dig, down to old-pixel size

    /// <summary>
    /// Smoothly shrinks the clickable cube to 'scale' times its size (1 = normal) over 'seconds'. Minigames that need the screen
    /// (the Sorting Race) use it so the cube doesn't cover their pieces, then call it again with 1 to bring it back.
    /// </summary>
    /// <summary>True while an event has shrunk the cube away (a takeover minigame): the auto clicker pauses.</summary>
    public bool CubeHidden => cubeShrinkTarget < 0.5f;

    public void SetCubeShrink(float scale, float seconds = 0.5f)
    {
        cubeShrinkTarget = Mathf.Clamp(scale, 0.02f, 1f);
        cubeShrinkSeconds = seconds;
    }

    private int liveNeed;           // clicks the pixel on the cube needs (only for random-click pixels, i.e. the Treasure chest)

    private static int RollNeededClicks(PixelTier tier)
    {
        int lo = Mathf.Max(1, Mathf.Min(tier.randomClicksMin, tier.randomClicksMax));
        int hi = Mathf.Max(lo, Mathf.Max(tier.randomClicksMin, tier.randomClicksMax));
        return UnityEngine.Random.Range(lo, hi + 1);
    }

    /// <summary>How many clicks the pixel on the cube needs: the tier's own number, or this chest's random one.</summary>
    private int NeededClicks(PixelTier tier)
    {
        if (!tier.randomClicks) return tier.clicksToCollect;
        if (liveNeed < 1) liveNeed = RollNeededClicks(tier);
        return liveNeed;
    }

    private bool liveRich;          // the ore on the cube is a rich vein
    private GameObject richGlitter;

    private void Materialize(PixelTier tier)
    {
        using (materializeMarker.Auto()) MaterializeInner(tier);
        PixelMaterialized?.Invoke(System.Array.IndexOf(tiers, tier));
    }

    private void MaterializeInner(PixelTier tier)
    {
        if (IsHazard(tier.type)) PixelHints.Trigger("hazard");
        hitsOnCurrentPixel = 0; // a fresh pixel has taken no hits yet
        fishNeed = fishReel = 0;
        digShrinkTarget = digShrinkCurrent = 1f;
        defuseClicks = 0;
        liveSince = Time.time;
        liveNeed = tier.randomClicks ? RollNeededClicks(tier) : 0;   // a treasure chest needs a random number of clicks

        // A fresh ore pixel is sometimes a rich vein: it glitters and pays much more when it breaks.
        if (richGlitter != null) { Destroy(richGlitter); richGlitter = null; }
        liveRich = IsOre(tier.type) && !disableProspectorFx && UnityEngine.Random.value < (richVeinChance > 0f ? richVeinChance : 0.08f);
        if (liveRich && pixelTransform != null) richGlitter = OreFx.Glitter(pixelTransform, PixelBaseSize);
        if (materializeRoutine != null) StopCoroutine(materializeRoutine);
        materializeRoutine = StartCoroutine(MaterializeRoutine(tier));
        if (pendingHits > 0)
        {
            // A block that comes back from the hold keeps its damage (and its cracks).
            hitsOnCurrentPixel = Mathf.Clamp(pendingHits, 0, Mathf.Max(0, NeededClicks(tier) - 1));
            if (hitsOnCurrentPixel > 0) SetLiveDamage(tier, hitsOnCurrentPixel);
        }
        pendingHits = 0;
        liveStake = pendingStake;   // a staked block (Block Hold) comes back worth more... or nothing
        pendingStake = 1f;
        if (!Mathf.Approximately(liveStake, 1f)) StakeAppeared?.Invoke(liveStake);
    }

    private IEnumerator MaterializeRoutine(PixelTier tier)
    {
        isSpawning = true;
        ApplyTierLook(tier);
        materializeFactor = 0f;

        float t = 0f;
        while (t < materializeDuration)
        {
            t += AnimDelta;
            float k = materializeDuration > 0f ? Mathf.Clamp01(t / materializeDuration) : 1f;
            materializeFactor = materializeCurve.Evaluate(k);
            yield return null;
        }

        materializeFactor = 1f;
        isSpawning = false;
        materializeRoutine = null;
    }

    /// <summary>Removes every old pixel lying around (no payout). Used by the dev tools.</summary>
    /// <summary>
    /// Dev tools: sets every pixel count to 0 - the inventory amount, the lifetime total shown in the Log and the click count -
    /// but keeps what is unlocked (pixel types, shop packs, Ultra pixels and boosts, earned achievements).
    /// </summary>
    public void ResetAllCounts()
    {
        foreach (PixelTier t in tiers)
        {
            t.count = 0d;
            t.totalCollected = 0d;
            t.timesCollected = 0L;
        }
        NotifyChanged();
    }

    /// <summary>The cube's normal size in world units (not counting pulsing / materializing). Used to size things around it.</summary>
    public float PixelBaseSize
    {
        get
        {
            if (pixelTransform == null) return 1f;
            Vector3 s = baseScale;
            if (pixelTransform.parent != null) s = Vector3.Scale(s, pixelTransform.parent.lossyScale);
            return Mathf.Max(s.x, s.y, s.z);
        }
    }

    /// <summary>While true, clicks on the cube are ignored (used while a device is being placed).</summary>
    public void SetClicksBlocked(bool blocked) => clicksBlocked = blocked;

    private void PlayClickEffects(PixelTier tier)
    {
        if (clickParticles != null)
        {
            ParticleSystem.MainModule main = clickParticles.main;
            main.startColor = tier.color;
            clickParticles.transform.position = pixelTransform.position;
            clickParticles.Play();
        }

        PlaySound(tier.clickSound != null ? tier.clickSound : defaultClickSound);
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;
        audioSource.pitch = 1f + UnityEngine.Random.Range(-pitchVariation, pitchVariation);
        audioSource.PlayOneShot(clip, soundVolume);
    }
}
