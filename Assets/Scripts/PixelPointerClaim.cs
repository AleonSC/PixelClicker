// ---- System notes ----
// PixelPointerClaim.cs
//   One shared "this click belongs to something in the world, not the cube" flag. A device / visitor that has the pointer over a part
//   it reacts to calls PixelPointerClaim.Claim() every frame it has it (sorter handles, Magnetized Sorter pipes, Quantum Organizer, Storage
//   Crate, Recycler button, carry handles, Bait Bucket, the Egg, the Beanstalk's fruit, a Treasure Map spot); PixelClicker.ExternalClickBlock
//   reads Claimed (this frame or the last, so script order does not matter). Replaces the separate HandleHot / Hot flag each of them used to
//   have (and the matching list in ExternalClickBlock): a new device only calls Claim(). PixelRecycler.ButtonHot and PixelCarryHandles.Hot stay
//   as their own flags because Pixel Grabbing steps aside for exactly those. ClaimButton() = a BUTTON on a world object (the Sorting Hopper's
//   eject button and filter flag): it claims the pointer AND makes Pixel Grabbing step aside (ButtonClaimed, read by PixelGrab's guard) - a new
//   world button uses it instead of its own flag.
// ---------------------------------------------------
using UnityEngine;

/// <summary>Shared per-frame claim on the pointer: something in the world (a device's handle, a visitor) owns the next click, so the cube ignores it.</summary>
public static class PixelPointerClaim
{
    private static int frame = -10;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { frame = -10; buttonFrame = -10; }

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
