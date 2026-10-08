using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Mouse input that works with both Unity input systems (legacy Input Manager and the new Input System),
/// so scripts don't each carry their own copy of the #if blocks.
/// </summary>
public static class PixelInput
{
    /// <summary>Mouse position in screen pixels.</summary>
    public static Vector2 PointerPosition()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
        return Input.mousePosition;
#endif
    }

    /// <summary>Mouse wheel this frame in notches (positive = scrolled up).</summary>
    public static float ScrollY()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return Mouse.current != null ? Mouse.current.scroll.ReadValue().y / 120f : 0f;
#else
        return Input.mouseScrollDelta.y;
#endif
    }

    /// <summary>True on the frame the Q key went down.</summary>
    public static bool QPressed()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Q);
#endif
    }

    /// <summary>True while the left button is held down.</summary>
    public static bool LeftHeld()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return Mouse.current != null && Mouse.current.leftButton.isPressed;
#else
        return Input.GetMouseButton(0);
#endif
    }

    /// <summary>True on the frame the left button went down.</summary>
    public static bool LeftPressed()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    /// <summary>True while the right button is held down.</summary>
    public static bool RightHeld()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return Mouse.current != null && Mouse.current.rightButton.isPressed;
#else
        return Input.GetMouseButton(1);
#endif
    }

    /// <summary>True on the frame the right button went down.</summary>
    public static bool RightPressed()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(1);
#endif
    }

    /// <summary>True while the mouse is over a UI element (so clicks on UI don't also hit the game).</summary>
    public static bool PointerOverUI() => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
}
