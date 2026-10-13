// ---- System notes ----
// PixelRightTap.cs
//   A quick right CLICK on a target, told apart from a right HOLD on it (holding is used to delete tester objects, PixelHold). One instance per user: call Update(whatIsUnderThePointer) every frame the right
//   button could be involved (when RightPressed, RightHeld or Pending); it returns true on the frame the button is let go on the same target after at most maxSeconds. Used by the belt flip
//   (PixelConveyorSystem) and the Solar Panel's re-link (PixelSolarPanel), which used to fire on the press.
// ---------------------------------------------------
using UnityEngine;

public sealed class PixelRightTap
{
    private object target;
    private float down;

    /// <summary>A press is being tracked (the owner should keep calling Update until it ends).</summary>
    public bool Pending => target != null;

    public bool Update(object current, float maxSeconds = 0.35f)
    {
        if (PixelInput.RightPressed()) { target = current; down = Time.unscaledTime; return false; }
        if (target == null) return false;
        if (PixelInput.RightHeld())
        {
            if (Time.unscaledTime - down > maxSeconds) target = null;   // it is a hold now, not a click
            return false;
        }
        bool tap = current == target;
        target = null;
        return tap;
    }
}
