// ---- System notes ----
// PixelBody.cs
//   ONE place for reading / setting a Rigidbody's velocity across Unity versions (Unity 6 renamed velocity to linearVelocity). Use it instead of
//   writing the #if UNITY_6000_0_OR_NEWER block again.
//   - Velocity(rb): the body's velocity.
//   - SetVelocity(rb, v, stopSpin): sets it as is (stopSpin also zeroes the spin).
//   - Steer(rb, v, stopSpin): the same, but does nothing on a null or kinematic body (Unity warns when a kinematic body's velocity is set).
// ---------------------------------------------------
using UnityEngine;

public static class PixelBody
{
    /// <summary>The body's velocity.</summary>
    public static Vector3 Velocity(Rigidbody rb)
    {
#if UNITY_6000_0_OR_NEWER
        return rb.linearVelocity;
#else
        return rb.velocity;
#endif
    }

    /// <summary>Sets the body's velocity ('stopSpin' also zeroes its spin).</summary>
    public static void SetVelocity(Rigidbody rb, Vector3 v, bool stopSpin = false)
    {
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = v;
#else
        rb.velocity = v;
#endif
        if (stopSpin) rb.angularVelocity = Vector3.zero;
    }

    /// <summary>Sets the velocity of a body physics can move: nothing happens for a null or kinematic one (Unity warns when a kinematic body is steered).</summary>
    public static void Steer(Rigidbody rb, Vector3 v, bool stopSpin = false)
    {
        if (rb == null || rb.isKinematic) return;
        SetVelocity(rb, v, stopSpin);
    }
}
