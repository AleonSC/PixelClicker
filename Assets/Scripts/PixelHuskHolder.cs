// ---- System notes ----
// PixelHuskHolder.cs
//   ONE place for the "take a husk off the floor and keep it" steps the husk containers share (pulled out of the Recycler, Storage Crate and
//   Bait Bucket on 2026-10-17, when the Sorting Hopper needed the same steps a fourth time). A new container uses these, never its own copy:
//   - Takeable(clicker, body): a loose husk a container may take now - not vanishing, not kept / reserved by another machine (despawn.Held,
//     kinematic), not in the player's hand, a real pixel type, not a fly-away (Meteor) or a flying copy.
//   - Capture(clicker, body): takes it off the floor's husk list (ReleaseOldPixel without credit) and holds its fade timer (despawn.Held): it
//     stays a real physical husk (the Recycler keeps them like that). LetGo(clicker, body, holder): the reverse - unfrozen, its timer running
//     again (unless it is in the player's hand), back on the floor's husk list (AdoptOldPixel).
//   - Freeze / Unfreeze(body, holder): kinematic + parented to the holder, so it moves and scales with it (and back to a loose body).
//   - Reserve / Unreserve(body): a husk on its way somewhere (the Sorting Hopper's slide): kinematic with its timer held but still ON the list,
//     so Takeable says no and no other machine snatches it on the way.
//   - FlyIn(go, target, point, growTarget): the "shrink into the container" animation for containers that keep a husk as DATA (Storage Crate,
//     Bait Bucket): the body is switched off, slides to the target and is destroyed while the container's display model grows in its place. The
//     animation runs on the persistent husk ticker runner (PixelHuskTicker.Run), not on the container, so removing the container or loading a save
//     mid-flight never leaves a frozen, switched-off husk behind.
//   - SpawnIntoHand(clicker, tier, amount, at): a stored husk taken back out straight into the mouse's hand (Pixel Grabbing).
//   ONE OWNER PER HUSK (2026-10-17 Recycler rework): every machine that reaches for a husk - the Recycler's suction / suck-in / Capture, the Magnet, the
//   Sweeper, the Sorting Hopper - asks Takeable first, and the one that takes it marks it (Capture or Reserve: its timer Held, kinematic while carried);
//   from then on Takeable says no to everyone else, so whoever captures a husk first owns it. No device-to-device special cases.
//   Users: PixelRecycler, PixelStorageCrate, PixelBaitBucket, PixelSortingHopper, PixelSweeper, PixelMagnetDevice; Takeable also by every device that takes
//   or pushes loose husks (Fan, Vacuum Device, Wood Chipper, Magnetized Sorter, Quantum Organizer, Trash Can, Kiln, Funnel, Vacuum Cleaner).
// ---------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class PixelHuskHolder
{
    /// <summary>
    /// Can a machine take (or push) this loose husk now? Not vanishing, not held / reserved by something else, not in the hand, a real pixel type,
    /// not flying. 'allowLandedFlyAway': a fly-away type (Meteor) that has LANDED is fine too (the containers keep them out; the Fan, Vacuum Device,
    /// Wood Chipper, Kiln, Trash Can, Funnel, Magnetized Sorter and Vacuum Cleaner always took them).
    /// </summary>
    public static bool Takeable(PixelClicker clicker, Rigidbody body, bool allowLandedFlyAway = false)
    {
        if (clicker == null || body == null || body.isKinematic || clicker.IsFlyingPixel(body)) return false;
        PixelHuskQuery.Husk h = PixelHuskQuery.Get(body);
        if (h.despawn != null && (h.despawn.Held || h.despawn.IsDespawning)) return false;
        if (h.info == null || h.info.heldByPlayer || !clicker.IsValidTierIndex(h.info.tierIndex)) return false;
        return allowLandedFlyAway || !clicker.Tiers[h.info.tierIndex].flyAway;
    }

    /// <summary>Takes a husk off the floor's husk list and holds its fade timer; it stays a real body. False if it was not on the list.</summary>
    public static bool Capture(PixelClicker clicker, Rigidbody body)
    {
        if (clicker == null || !clicker.ReleaseOldPixel(body, false)) return false;
        OldPixelDespawn despawn = PixelHuskQuery.Get(body).despawn;
        if (despawn != null) despawn.Held = true;   // it never shrinks away while it is kept
        return true;
    }

    /// <summary>Hands a kept husk back to the floor: unfrozen, its timer running again (not while it is in the player's hand) and back on the husk list.</summary>
    public static void LetGo(PixelClicker clicker, Rigidbody body, Transform holder)
    {
        if (body == null) return;
        PixelHuskQuery.Husk h = PixelHuskQuery.Get(body);
        if (h.despawn != null && !h.HeldByPlayer) h.despawn.Held = false;
        Unfreeze(body, holder);
        if (clicker != null) clicker.AdoptOldPixel(body);   // grabbable, despawns and can be caught again
    }

    /// <summary>Freezes a husk into its holder: no velocity, kinematic and parented to it, so it moves and scales with the holder.</summary>
    public static void Freeze(Rigidbody body, Transform holder)
    {
        if (body == null) return;
        if (!body.isKinematic)
        {
            SetVelocity(body, Vector3.zero);
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
        }
        if (holder != null && body.transform.parent != holder) body.transform.SetParent(holder, true);
    }

    /// <summary>A loose physical body again (unparented from the holder).</summary>
    public static void Unfreeze(Rigidbody body, Transform holder)
    {
        if (body == null) return;
        if (holder != null && body.transform.parent == holder) body.transform.SetParent(null, true);
        if (body.isKinematic) body.isKinematic = false;
    }

    /// <summary>A husk on its way somewhere: kinematic, timer held, still on the husk list (no other machine takes it).</summary>
    public static void Reserve(Rigidbody body)
    {
        if (body == null) return;
        SetVelocity(body, Vector3.zero);
        body.angularVelocity = Vector3.zero;
        body.isKinematic = true;
        OldPixelDespawn despawn = PixelHuskQuery.Get(body).despawn;
        if (despawn != null) despawn.Held = true;
    }

    /// <summary>Ends Reserve: a loose body with its timer running (fresh: KeepAlive), moving at 'velocity'.</summary>
    public static void Unreserve(Rigidbody body, Vector3 velocity)
    {
        if (body == null) return;
        body.isKinematic = false;
        SetVelocity(body, velocity);
        OldPixelDespawn despawn = PixelHuskQuery.Get(body).despawn;
        if (despawn != null) { despawn.Held = false; if (!despawn.IsDespawning) despawn.KeepAlive(); }
    }

    public static void SetVelocity(Rigidbody body, Vector3 v) => PixelBody.SetVelocity(body, v);   // (PixelBody: the one version-safe velocity helper)

    /// <summary>
    /// The husk (already taken off the list) is switched off and slides into the container, shrinking away; 'target' (if any) is where it goes and,
    /// with growTarget, the container's display model that pops up from nothing as it arrives. Without a target it slides to 'point'. Run with StartCoroutine:
    /// the first step hands the animation to the persistent husk ticker runner (PixelHuskTicker.Run), so it always finishes and destroys the husk even when
    /// the container is removed (or a save loads) mid-flight - a husk already off the list and switched off is never left frozen in the scene.
    /// </summary>
    public static IEnumerator FlyIn(GameObject go, Transform target, Vector3 point, bool growTarget, float seconds = 0.28f)
    {
        if (go != null) PixelHuskTicker.Run(FlyInSteps(go, target, point, growTarget, seconds));
        yield break;
    }

    private static IEnumerator FlyInSteps(GameObject go, Transform target, Vector3 point, bool growTarget, float seconds)
    {
        if (go == null) yield break;
        foreach (MonoBehaviour mb in go.GetComponents<MonoBehaviour>()) if (mb != null) mb.enabled = false;   // (its fade timer too)
        Rigidbody rb = go.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;
        foreach (Collider c in go.GetComponentsInChildren<Collider>()) c.enabled = false;
        foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>(true)) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        Vector3 start = go.transform.position, scale = go.transform.localScale;
        bool grow = growTarget && target != null;
        Vector3 finalScale = grow ? target.localScale : Vector3.one;
        if (grow) target.localScale = Vector3.zero;
        float t = 0f, length = Mathf.Max(0.01f, seconds);
        Vector3 end = target != null ? target.position : point;
        while (t < length)
        {
            if (go == null) yield break;
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / length));
            if (target != null) end = target.position;   // (the container may be gone mid-flight: it carries on to where the target last was)
            go.transform.position = Vector3.Lerp(start, end, k);
            go.transform.localScale = scale * (1f - k);
            if (grow && target != null) target.localScale = finalScale * k;
            yield return null;
        }
        if (grow && target != null) target.localScale = finalScale;
        if (go != null) Object.Destroy(go);
    }

    /// <summary>Spawns a stored husk back at 'at' and puts it straight in the mouse's hand (Pixel Grabbing). Returns the new husk (null if none was made).</summary>
    public static Rigidbody SpawnIntoHand(PixelClicker clicker, int tier, double amount, Vector3 at)
    {
        if (clicker == null || !clicker.SpawnStoredPixel(tier, amount, at, Vector3.zero)) return null;
        IReadOnlyList<Rigidbody> pixels = clicker.OldPixels;
        Rigidbody body = pixels.Count > 0 ? pixels[pixels.Count - 1] : null;
        PixelGrab grab = PixelFind.Cached<PixelGrab>();
        if (body != null && grab != null) grab.BeginHold(body);
        return body;
    }
}
