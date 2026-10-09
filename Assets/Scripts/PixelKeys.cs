using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

/// <summary>The keyboard actions the player can rebind (Settings > Key Bindings).</summary>
public enum PixelAction
{
    TimeStop = 0,
    Hose = 1,
    RotateLeft = 2,
    RotateRight = 3,
    EventLog = 4,
    TimeSlow = 5,
}

/// <summary>
/// The rebindable keys: defaults, the player's choices (saved in PlayerPrefs), and reading them with either input system.
/// Other scripts ask <c>PixelKeys.Pressed(PixelAction.TimeStop)</c> instead of checking a fixed key. Texts can contain
/// <c>{key:TimeStop}</c> style placeholders; <see cref="Replace"/> fills in the current key names.
/// </summary>
public static class PixelKeys
{
    private static readonly KeyCode[] Defaults =
    {
        KeyCode.T,        // TimeStop
        KeyCode.B,        // Hose
        KeyCode.Q,        // RotateLeft
        KeyCode.E,        // RotateRight
        KeyCode.Return,   // EventLog
        KeyCode.S,        // TimeSlow
    };

    private const string PrefPrefix = "PixelClicker.Key.";
    private static KeyCode[] current;

    /// <summary>Raised after any binding changes (so texts can refresh).</summary>
    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        current = null;
        Changed = null;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        reverse = null;
#endif
    }

    public static int Count => Defaults.Length;

    /// <summary>A save file with its own settings was loaded: read every binding again.</summary>
    public static void ReloadFromPrefs()
    {
        current = null;
        Load();
        Changed?.Invoke();
    }

    private static void Load()
    {
        if (current != null) return;
        current = new KeyCode[Defaults.Length];
        for (int i = 0; i < current.Length; i++)
        {
            int saved = PlayerPrefs.GetInt(PrefPrefix + (PixelAction)i, (int)Defaults[i]);
            current[i] = Enum.IsDefined(typeof(KeyCode), saved) ? (KeyCode)saved : Defaults[i];
        }
    }

    public static KeyCode Get(PixelAction action)
    {
        Load();
        return current[(int)action];
    }

    public static KeyCode DefaultOf(PixelAction action) => Defaults[(int)action];

    /// <summary>Binds a key to an action. If another action already uses that key, the two swap keys.</summary>
    public static void Set(PixelAction action, KeyCode key)
    {
        Load();
        int index = (int)action;
        KeyCode old = current[index];
        for (int i = 0; i < current.Length; i++)
        {
            if (i != index && current[i] == key)
            {
                current[i] = old; // swap, so no two actions share a key
                Save(i);
            }
        }
        current[index] = key;
        Save(index);
        Changed?.Invoke();
    }

    public static void ResetAll()
    {
        Load();
        for (int i = 0; i < current.Length; i++)
        {
            current[i] = Defaults[i];
            Save(i);
        }
        Changed?.Invoke();
    }

    private static void Save(int index)
    {
        PlayerPrefs.SetInt(PrefPrefix + (PixelAction)index, (int)current[index]);
        PlayerPrefs.Save();
    }

    /// <summary>The name of an action's key as shown to the player.</summary>
    public static string Name(PixelAction action) => KeyName(Get(action));

    public static string KeyName(KeyCode key)
    {
        switch (key)
        {
            case KeyCode.Return: return "Enter";
            case KeyCode.KeypadEnter: return "Num Enter";
            case KeyCode.Alpha0: return "0";
            case KeyCode.Alpha1: return "1";
            case KeyCode.Alpha2: return "2";
            case KeyCode.Alpha3: return "3";
            case KeyCode.Alpha4: return "4";
            case KeyCode.Alpha5: return "5";
            case KeyCode.Alpha6: return "6";
            case KeyCode.Alpha7: return "7";
            case KeyCode.Alpha8: return "8";
            case KeyCode.Alpha9: return "9";
            case KeyCode.LeftControl: return "Left Ctrl";
            case KeyCode.RightControl: return "Right Ctrl";
            case KeyCode.LeftShift: return "Left Shift";
            case KeyCode.RightShift: return "Right Shift";
            case KeyCode.LeftAlt: return "Left Alt";
            case KeyCode.RightAlt: return "Right Alt";
            case KeyCode.BackQuote: return "`";
        }
        return key.ToString();
    }

    // ------------------------------------------------------------------
    // Text placeholders
    // ------------------------------------------------------------------

    /// <summary>
    /// Replaces {key:Action} placeholders with the current key names. Older texts that still spell out the default keys
    /// ("Q / E", "Press T to freeze time", "press B for the hose") are updated too.
    /// </summary>
    public static string Replace(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        for (int i = 0; i < Defaults.Length; i++)
        {
            string token = "{key:" + (PixelAction)i + "}";
            if (text.Contains(token)) text = text.Replace(token, Name((PixelAction)i));
        }

        string left = Name(PixelAction.RotateLeft), right = Name(PixelAction.RotateRight);
        text = text.Replace("Q / E", left + " / " + right).Replace("Q or E", left + " or " + right);
        text = text.Replace("Press T to freeze time and T again", "Press " + Name(PixelAction.TimeStop) + " to freeze time and " + Name(PixelAction.TimeStop) + " again");
        text = text.Replace("press B for the hose", "press " + Name(PixelAction.Hose) + " for the hose");
        return text;
    }

    // ------------------------------------------------------------------
    // Reading the keys (both input systems)
    // ------------------------------------------------------------------

    /// <summary>True on the frame the action's key went down.</summary>
    public static bool Pressed(PixelAction action)
    {
        KeyCode key = Get(action);
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        if (Control(key, out var c) && c.wasPressedThisFrame) return true;
        if (key == KeyCode.Return && Control(KeyCode.KeypadEnter, out var n) && n.wasPressedThisFrame) return true;
        return false;
#else
        return Input.GetKeyDown(key) || (key == KeyCode.Return && Input.GetKeyDown(KeyCode.KeypadEnter));
#endif
    }

    /// <summary>True while the action's key is held.</summary>
    public static bool Held(PixelAction action)
    {
        KeyCode key = Get(action);
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return Control(key, out var c) && c.isPressed;
#else
        return Input.GetKey(key);
#endif
    }

    // ------------------------------------------------------------------
    // Capturing a key for the rebinding screen
    // ------------------------------------------------------------------

    /// <summary>
    /// Call every frame while waiting for the player to press a key. Returns true with the key once one went down
    /// (mouse buttons are ignored).
    /// </summary>
    public static bool TryCapture(out KeyCode key)
    {
        key = KeyCode.None;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        Keyboard kb = Keyboard.current;
        if (kb == null) return false;
        BuildReverse();
        foreach (var k in kb.allKeys)
        {
            if (!k.wasPressedThisFrame) continue;
            if (reverse.TryGetValue(k.keyCode, out KeyCode code)) { key = code; return true; }
        }
        return false;
#else
        if (!Input.anyKeyDown) return false;
        foreach (KeyCode k in Enum.GetValues(typeof(KeyCode)))
        {
            if (k == KeyCode.None || (k >= KeyCode.Mouse0 && k <= KeyCode.Mouse6)) continue;
            if (k >= KeyCode.JoystickButton0) continue;
            if (Input.GetKeyDown(k)) { key = k; return true; }
        }
        return false;
#endif
    }

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
    private static Dictionary<Key, KeyCode> reverse;

    private static void BuildReverse()
    {
        if (reverse != null) return;
        reverse = new Dictionary<Key, KeyCode>();
        foreach (KeyCode code in Enum.GetValues(typeof(KeyCode)))
            if (ToKey(code, out Key key) && !reverse.ContainsKey(key)) reverse[key] = code;
    }

    private static bool Control(KeyCode code, out UnityEngine.InputSystem.Controls.KeyControl control)
    {
        control = null;
        Keyboard kb = Keyboard.current;
        if (kb == null || !ToKey(code, out Key key)) return false;
        control = kb[key];
        return control != null;
    }

    /// <summary>Converts a legacy KeyCode to the new Input System's Key (they mostly share names).</summary>
    private static bool ToKey(KeyCode code, out Key key)
    {
        string name = code.ToString();
        if (name.StartsWith("Alpha")) name = "Digit" + name.Substring(5);
        else if (name.StartsWith("Keypad"))
        {
            string rest = name.Substring(6);
            name = rest == "Enter" ? "NumpadEnter" : "Numpad" + rest;
        }
        else if (name == "Return") name = "Enter";
        else if (name == "LeftControl") name = "LeftCtrl";
        else if (name == "RightControl") name = "RightCtrl";
        else if (name == "LeftApple" || name == "LeftCommand") name = "LeftMeta";
        else if (name == "RightApple" || name == "RightCommand") name = "RightMeta";
        return Enum.TryParse(name, true, out key) && key != Key.None;
    }
#endif
}
