// Stand-in for the Input System package (com.unity.inputsystem). Only compiled into the
// PlayerInputSystem check variant's #if ENABLE_INPUT_SYSTEM paths.
// Signatures mirror the real API; bodies are empty. Only add members that exist in Unity.
using UnityEngine.EventSystems;

namespace UnityEngine.InputSystem
{
    public abstract class InputControl
    {
        public string name => null;
        public string displayName => null;
        public string path => null;
        public InputDevice device => null;
        public abstract object ReadValueAsObject();
    }
    public abstract class InputControl<TValue> : InputControl where TValue : struct
    {
        public TValue ReadValue() => default;
        public TValue ReadDefaultValue() => default;
        public override object ReadValueAsObject() => null;
    }
    public class InputDevice : InputControl<byte>
    {
        public bool added => false;
        public bool enabled => false;
        public int deviceId => 0;
        public double lastUpdateTime => 0;
        public bool wasUpdatedThisFrame => false;
    }
}

namespace UnityEngine.InputSystem.Controls
{
    public class AxisControl : InputControl<float> { }
    public class ButtonControl : AxisControl
    {
        public bool isPressed => false;
        public bool wasPressedThisFrame => false;
        public bool wasReleasedThisFrame => false;
        public float pressPointOrDefault => 0f;
    }
    public class KeyControl : ButtonControl
    {
        public Key keyCode => default;
        public int scanCode => 0;
    }
    public class AnyKeyControl : ButtonControl { }
    public class Vector2Control : InputControl<Vector2>
    {
        public AxisControl x => null;
        public AxisControl y => null;
    }
    public class DeltaControl : Vector2Control
    {
        public AxisControl up => null;
        public AxisControl down => null;
        public AxisControl left => null;
        public AxisControl right => null;
    }
    public class IntegerControl : InputControl<int> { }
}

namespace UnityEngine.InputSystem
{
    using UnityEngine.InputSystem.Controls;

    public class Pointer : InputDevice
    {
        public static Pointer current => null;
        public Vector2Control position => null;
        public DeltaControl delta => null;
        public Vector2Control radius => null;
        public AxisControl pressure => null;
        public ButtonControl press => null;
        public IntegerControl displayIndex => null;
    }
    public class Mouse : Pointer
    {
        public new static Mouse current => null;
        public DeltaControl scroll => null;
        public ButtonControl leftButton => null;
        public ButtonControl middleButton => null;
        public ButtonControl rightButton => null;
        public ButtonControl backButton => null;
        public ButtonControl forwardButton => null;
        public IntegerControl clickCount => null;
        public void WarpCursorPosition(Vector2 position) { }
    }
    public class Keyboard : InputDevice
    {
        public static Keyboard current => null;
        public AnyKeyControl anyKey => null;
        public KeyControl this[Key key] => null;
        public KeyControl spaceKey => null; public KeyControl enterKey => null; public KeyControl tabKey => null;
        public KeyControl backquoteKey => null; public KeyControl quoteKey => null; public KeyControl semicolonKey => null;
        public KeyControl commaKey => null; public KeyControl periodKey => null; public KeyControl slashKey => null;
        public KeyControl backslashKey => null; public KeyControl leftBracketKey => null; public KeyControl rightBracketKey => null;
        public KeyControl minusKey => null; public KeyControl equalsKey => null;
        public KeyControl aKey => null; public KeyControl bKey => null; public KeyControl cKey => null; public KeyControl dKey => null;
        public KeyControl eKey => null; public KeyControl fKey => null; public KeyControl gKey => null; public KeyControl hKey => null;
        public KeyControl iKey => null; public KeyControl jKey => null; public KeyControl kKey => null; public KeyControl lKey => null;
        public KeyControl mKey => null; public KeyControl nKey => null; public KeyControl oKey => null; public KeyControl pKey => null;
        public KeyControl qKey => null; public KeyControl rKey => null; public KeyControl sKey => null; public KeyControl tKey => null;
        public KeyControl uKey => null; public KeyControl vKey => null; public KeyControl wKey => null; public KeyControl xKey => null;
        public KeyControl yKey => null; public KeyControl zKey => null;
        public KeyControl digit1Key => null; public KeyControl digit2Key => null; public KeyControl digit3Key => null;
        public KeyControl digit4Key => null; public KeyControl digit5Key => null; public KeyControl digit6Key => null;
        public KeyControl digit7Key => null; public KeyControl digit8Key => null; public KeyControl digit9Key => null; public KeyControl digit0Key => null;
        public KeyControl leftShiftKey => null; public KeyControl rightShiftKey => null; public KeyControl leftAltKey => null; public KeyControl rightAltKey => null;
        public KeyControl leftCtrlKey => null; public KeyControl rightCtrlKey => null; public KeyControl leftMetaKey => null; public KeyControl rightMetaKey => null;
        public KeyControl escapeKey => null; public KeyControl leftArrowKey => null; public KeyControl rightArrowKey => null;
        public KeyControl upArrowKey => null; public KeyControl downArrowKey => null; public KeyControl backspaceKey => null;
        public KeyControl pageDownKey => null; public KeyControl pageUpKey => null; public KeyControl homeKey => null; public KeyControl endKey => null;
        public KeyControl insertKey => null; public KeyControl deleteKey => null; public KeyControl capsLockKey => null;
        public KeyControl numLockKey => null; public KeyControl printScreenKey => null; public KeyControl scrollLockKey => null; public KeyControl pauseKey => null;
        public KeyControl numpadEnterKey => null; public KeyControl numpadDivideKey => null; public KeyControl numpadMultiplyKey => null;
        public KeyControl numpadPlusKey => null; public KeyControl numpadMinusKey => null; public KeyControl numpadPeriodKey => null; public KeyControl numpadEqualsKey => null;
        public KeyControl numpad0Key => null; public KeyControl numpad1Key => null; public KeyControl numpad2Key => null; public KeyControl numpad3Key => null;
        public KeyControl numpad4Key => null; public KeyControl numpad5Key => null; public KeyControl numpad6Key => null; public KeyControl numpad7Key => null;
        public KeyControl numpad8Key => null; public KeyControl numpad9Key => null;
        public KeyControl f1Key => null; public KeyControl f2Key => null; public KeyControl f3Key => null; public KeyControl f4Key => null;
        public KeyControl f5Key => null; public KeyControl f6Key => null; public KeyControl f7Key => null; public KeyControl f8Key => null;
        public KeyControl f9Key => null; public KeyControl f10Key => null; public KeyControl f11Key => null; public KeyControl f12Key => null;
        public ButtonControl shiftKey => null; public ButtonControl ctrlKey => null; public ButtonControl altKey => null;
    }

    public enum Key
    {
        None, Space, Enter, Tab, Backquote, Quote, Semicolon, Comma, Period, Slash, Backslash, LeftBracket, RightBracket, Minus, Equals,
        A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
        Digit1, Digit2, Digit3, Digit4, Digit5, Digit6, Digit7, Digit8, Digit9, Digit0,
        LeftShift, RightShift, LeftAlt, RightAlt, AltGr = RightAlt, LeftCtrl, RightCtrl, LeftMeta, RightMeta,
        LeftWindows = LeftMeta, RightWindows = RightMeta, LeftApple = LeftMeta, RightApple = RightMeta, LeftCommand = LeftMeta, RightCommand = RightMeta,
        ContextMenu, Escape, LeftArrow, RightArrow, UpArrow, DownArrow, Backspace, PageDown, PageUp, Home, End, Insert, Delete,
        CapsLock, NumLock, PrintScreen, ScrollLock, Pause,
        NumpadEnter, NumpadDivide, NumpadMultiply, NumpadPlus, NumpadMinus, NumpadPeriod, NumpadEquals,
        Numpad0, Numpad1, Numpad2, Numpad3, Numpad4, Numpad5, Numpad6, Numpad7, Numpad8, Numpad9,
        F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
        OEM1, OEM2, OEM3, OEM4, OEM5, IMESelected
    }
}

namespace UnityEngine.InputSystem.UI
{
    public class InputSystemUIInputModule : BaseInputModule
    {
        public override void Process() { }
        public void AssignDefaultActions() { }
        public void UnassignActions() { }
    }
}
