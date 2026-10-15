// ---- System notes ----
// PixelPointerClaim.cs
//   One shared "this click belongs to something in the world, not the cube" flag. A device / visitor that has the pointer over a part
//   it reacts to calls PixelPointerClaim.Claim() every frame it has it (sorter handles, Magnetized Sorter pipes, Quantum Organizer, Storage
//   Crate, Recycler button, carry handles, Bait Bucket, the Egg, the Beanstalk's fruit, a Treasure Map spot); PixelClicker.ExternalClickBlock
//   reads Claimed (this frame or the last, so script order does not matter). Replaces the separate HandleHot / Hot flag each of them used to
//   have (and the matching list in ExternalClickBlock): a new device only calls Claim(). PixelRecycler.ButtonHot and PixelCarryHandles.Hot stay
//   as their own flags because Pixel Grabbing steps aside for exactly those.
// ---------------------------------------------------
using UnityEngine;

/// <summary>Shared per-frame claim on the pointer: something in the world (a device's handle, a visitor) owns the next click, so the cube ignores it.</summary>
public static class PixelPointerClaim
{
    private static int frame = -10;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { frame = -10; }

    /// <summary>Claims the pointer for this frame (call every frame the pointer is on your part). Returns the frame number.</summary>
    public static int Claim() => frame = Time.frameCount;

    /// <summary>Did anything claim the pointer this frame or the last?</summary>
    public static bool Claimed => frame >= Time.frameCount - 1;
}
