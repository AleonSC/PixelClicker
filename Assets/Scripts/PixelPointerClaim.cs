// ---- System notes ----
// PixelPointerClaim.cs
//   One shared "this click belongs to something in the world, not the cube" flag. A device / visitor that has the pointer over a part
//   it reacts to calls PixelPointerClaim.Claim() every frame it has it (Quantum Organizer, Storage
//   Crate, Recycler button, carry handles, Bait Bucket, the Egg, the Beanstalk's fruit, a Treasure Map spot); PixelClicker.ExternalClickBlock
//   reads Claimed (this frame or the last, so script order does not matter). Replaces the separate HandleHot / Hot flag each of them used to
//   have (and the matching list in ExternalClickBlock): a new device only calls Claim(). PixelRecycler.ButtonHot and PixelCarryHandles.Hot stay
//   as their own flags because Pixel Grabbing steps aside for exactly those. ClaimButton() = a BUTTON on a world object (the Sorting Hopper's
//   eject button and filter flag): it claims the pointer AND makes Pixel Grabbing step aside (ButtonClaimed, read by PixelGrab's guard) - a new
//   world button uses it instead of its own flag.
//   PointerOn(clicker, body) = is the pointer on these colliders, with no husk or the cube in front of them (the Crafting Cube, the delivery crate).
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

/// <summary>Shared per-frame claim on the pointer: something in the world (a device's handle, a visitor) owns the next click, so the cube ignores it.</summary>
public static class PixelPointerClaim
{
    private static int frame = -10;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { frame = -10; buttonFrame = -10; }

    /// <summary>Is the pointer on one of these colliders (a world object's body), with nothing of the cube or a husk in front of it, and not over UI?</summary>
    public static bool PointerOn(PixelClicker clicker, IReadOnlyList<Collider> body)
    {
        Camera cam = clicker != null ? clicker.ViewCamera : null;
        if (cam == null || body == null || PixelInput.PointerOverUI()) return false;
        Ray ray = cam.ScreenPointToRay(PixelInput.PointerPosition());
        float best = float.MaxValue;
        for (int i = 0; i < body.Count; i++) if (body[i] != null && body[i].Raycast(ray, out RaycastHit hit, 500f)) best = Mathf.Min(best, hit.distance);
        if (best == float.MaxValue) return false;
        foreach (RaycastHit h in Physics.RaycastAll(ray, best, ~0, QueryTriggerInteraction.Ignore))
        {
            if (clicker.PixelTransform != null && h.transform.IsChildOf(clicker.PixelTransform)) return false;
            if (h.collider.GetComponentInParent<OldPixelInfo>() != null) return false;
        }
        return true;
    }

    /// <summary>Claims the pointer for this frame (call every frame the pointer is on your part). Returns the frame number.</summary>
    public static int Claim() => frame = Time.frameCount;

    /// <summary>Did anything claim the pointer this frame or the last?</summary>
    public static bool Claimed => frame >= Time.frameCount - 1;

    private static int buttonFrame = -10;

    /// <summary>Claims the pointer for a BUTTON on a world object: the cube ignores the click AND Pixel Grabbing steps aside. Call every frame the pointer is on it.</summary>
    public static int ClaimButton() { buttonFrame = Time.frameCount; return Claim(); }

    /// <summary>Is the pointer on a world button (this frame or the last)? Pixel Grabbing steps aside then.</summary>
    public static bool ButtonClaimed => buttonFrame >= Time.frameCount - 1;
}
