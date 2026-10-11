using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Keyboard shortcuts for the docked menu buttons (Inventory, Shop, Log, Crafting, Toggles, Bank, Skills). Each window registers its
/// button here; the keys are rebindable in Settings > Key Bindings and start out blank. A key only works while its button exists and is
/// available (a locked tab has no shortcut), never while the game is paused, a title screen / popup is up or a text box has the keyboard.
/// </summary>
public class PixelMenuKeys : MonoBehaviour
{
    private static readonly Dictionary<PixelAction, Button> buttons = new Dictionary<PixelAction, Button>();
    private static PixelMenuKeys instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        buttons.Clear();
        instance = null;
    }

    /// <summary>Makes a key open / close a window by clicking its button. Creates the component when the first button registers.</summary>
    public static void Register(PixelAction action, Button button)
    {
        if (button == null) return;
        buttons[action] = button;
        if (instance == null)
        {
            instance = new GameObject("Pixel Menu Keys").AddComponent<PixelMenuKeys>();
            DontDestroyOnLoad(instance.gameObject);
        }
    }

    private static readonly PixelAction[] MenuActions =
    {
        PixelAction.Inventory, PixelAction.Shop, PixelAction.LogWindow, PixelAction.Crafting,
        PixelAction.Toggles, PixelAction.Bank, PixelAction.Skills,
    };

    private void Update()
    {
        if (PixelKeys.Typing || PixelPauseMenu.IsPaused || PixelTitleScreen.Showing) return;
        foreach (PixelAction action in MenuActions)
        {
            if (PixelKeys.Get(action) == KeyCode.None || !PixelKeys.Pressed(action)) continue;
            if (!buttons.TryGetValue(action, out Button b) || b == null || !b.gameObject.activeInHierarchy || !b.interactable) continue;
            b.onClick.Invoke();
            return;
        }
    }
}
