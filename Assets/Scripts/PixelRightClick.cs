// ---- System notes ----
// PixelRightClick.cs
//   ONE place that decides who a RIGHT click in the world belongs to (added with Husk Crush, 2026-10-17). Each user asks PixelRightClick.Current
//   (worked out once per frame) instead of stacking its own checks. In order of priority:
//   1. Blocked: a menu, the pointer over UI.
//   2. Tool: the Bank hose is out, a device is being placed, the Conveyor Belt Kit / a tester piece is placing - they keep the right button.
//   3. Pet: a pet is under the pointer (or held / its menu is open) - right-click opens its menu (PixelPetsMenu).
//   4. WorldPart: anything else holding the mouse (PixelClicker.ExternalClickBlock: a device part that claimed the pointer, the UFO Helper,
//      first person, the new-game intro, dev pickers...).
//   5. Cube: the live cube is under the pointer, even with a husk lying in front of it - Block Hold, clearing a hazard, the shuffle tap.
//   6. Blocked again: a takeover minigame (its husks are its pieces), the camera intro, time frozen by something other than Time Stop.
//   7. Husk: the nearest thing under the pointer is a husk that can be crushed (PixelHuskCrush.CanCrush) - Husk Crush. Husk = that body.
//   8. None.
//   THE PRESS KEEPS ITS TARGET (2026-10-17, Charge Crush): the owner worked out on the frame the right button goes down is remembered (PressOwner)
//   while it stays held. A press that started on a husk stays the husk's: Current keeps answering Husk (Husk = that body, null once it is gone)
//   until the button is let go, so a hold that drifts onto the cube never starts Block Hold (its deferred start re-asks Current while held) and
//   a hold on the cube never turns into a husk charge (PixelHuskCrush only charges when PressOwner is Husk). The press is caught by whoever asks
//   first that frame; PixelHuskCrush asks on every press frame, so it is always caught. Released = PressOwner None again.
//   Users: PixelBlockHold.CanStart (Cube), PixelClicker's hazard / bottle right-click (Cube), PixelHuskCrush (Husk). Systems that still check the
//   right button on their own (belt flip, device / tester-object removal holds, the pet menu itself) are covered by the order above; a NEW right-click
//   use adds its owner here instead of its own check.
// ---------------------------------------------------
using UnityEngine;

public static class PixelRightClick
{
    public enum Owner { None, Blocked, Tool, Pet, WorldPart, Cube, Husk }

    private static int frame = -1;
    private static Owner owner, pressOwner;
    private static Rigidbody husk, pressHusk;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { frame = -1; owner = pressOwner = Owner.None; husk = pressHusk = null; }

    /// <summary>Who a right click at the pointer belongs to this frame.</summary>
    public static Owner Current { get { Resolve(); return owner; } }

    /// <summary>The husk a right click would crush this frame (null unless Current is Husk).</summary>
    public static Rigidbody Husk { get { Resolve(); return owner == Owner.Husk ? husk : null; } }

    /// <summary>Who the right button's current press belongs to (worked out on the frame it went down; None when the button is up).</summary>
    public static Owner PressOwner { get { Resolve(); return pressOwner; } }

    private static void Resolve()
    {
        if (frame == Time.frameCount) return;
        frame = Time.frameCount;
        bool pressed = PixelInput.RightPressed(), held = PixelInput.RightHeld();
        if (!pressed && held && pressOwner == Owner.Husk) { owner = Owner.Husk; husk = pressHusk; return; }   // a hold that started on a husk stays the husk's
        husk = null;
        owner = Decide(PixelFind.Cached<PixelClicker>());
        if (pressed) { pressOwner = owner; pressHusk = husk; }
        else if (!held) { pressOwner = Owner.None; pressHusk = null; }
    }

    private static Owner Decide(PixelClicker clicker)
    {
        if (clicker == null || PixelCalm.InMenu || PixelInput.PointerOverUI()) return Owner.Blocked;
        PixelConsumables consumables = PixelFind.Cached<PixelConsumables>();
        if (PixelBank.HoseOn || PixelFeatureTester.Placing || (consumables != null && consumables.IsPlacing)) return Owner.Tool;
        if (PixelPets.HoveringPet) return Owner.Pet;
        if (PixelClicker.ExternalClickBlock || PixelClicker.GodMode) return Owner.WorldPart;
        Vector2 screen = PixelInput.PointerPosition();
        if (clicker.IsLiveCubeAt(screen)) return Owner.Cube;
        if (PixelMinigame.TakeoverActive || PixelCameraIntro.ClicksLocked || (Time.timeScale <= 0f && !PixelTimeStop.IsStopped)) return Owner.Blocked;
        if (clicker.TryGetBlockAt(screen, out _, out bool live, out Rigidbody body) && !live && PixelHuskCrush.CanCrush(clicker, body))
        {
            husk = body;
            return Owner.Husk;
        }
        return Owner.None;
    }
}
