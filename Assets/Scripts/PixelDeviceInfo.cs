// ---- System notes ----
// PixelDeviceInfo.cs
//   What a placed device tells the shared device card / panel (PixelDeviceCard). ONE system for every device: nothing builds a card of its own.
//   * PixelDeviceInfo: the filled-in sheet - Title, Lines (rich text, 'label: value'), an optional Progress 0..1 with its label, and Full (false =
//     the small hover card: only the essentials; true = the right-click panel: everything). Reused (Clear) every refresh.
//   * IPixelDeviceInfo: a component on a placed device's root that adds lines (FillDeviceInfo). PixelPlacedDevice.BuildInfo writes the common part
//     (name, time / uses left with the progress bar, 'Click to start', powered, attuned; Full: run time, reach, Operation levels), then its own
//     virtual AddInfo (subclasses: Storage Crate, Quantum Organizer...), then every IPixelDeviceInfo on the same object (the behaviour components
//     next to a thin device stub: PixelRecycler, PixelSortingHopper, PixelSweeper).
//   * ACTIONS (2026-10-18): a device can add its own buttons to the right-click panel with info.AddAction(label, onClick, role) when info.Full
//     (the Sorting Hopper's 'Select filter' / 'Empty out'); the panel shows them two to a row above Remove / Upgrades.
//   * STEPPERS (2026-10-18): '- value +' rows in the panel (info.AddStepper): the Operation upgrades' level in use (PixelShop.AddOperationLevels),
//     so a player with every level bought can go back to level 3.
//   A new device: override AddInfo (a PixelPlacedDevice subclass) or implement IPixelDeviceInfo (a behaviour component); add the 2-3 key stats
//   always and the rest only when info.Full (run time, what it did this session...).
// ---------------------------------------------------
using System.Collections.Generic;

/// <summary>A component on a placed device that adds its lines to the shared device card / panel.</summary>
public interface IPixelDeviceInfo
{
    /// <summary>Add lines (info.Add / info.Note); info.Full = the right-click panel (everything), else the hover card (the essentials).</summary>
    void FillDeviceInfo(PixelDeviceInfo info);
}

/// <summary>The sheet a placed device fills in for the shared card / panel.</summary>
public sealed class PixelDeviceInfo
{
    /// <summary>The device's name (the card's title).</summary>
    public string Title = "";
    /// <summary>The lines under the title (rich text).</summary>
    public readonly List<string> Lines = new List<string>();
    /// <summary>0..1 for the bar under the lines (time left, how full), or below 0 = no bar.</summary>
    public float Progress = -1f;
    /// <summary>Text on the bar.</summary>
    public string ProgressLabel = "";
    /// <summary>True for the right-click panel (all the stats), false for the hover card (the essentials only).</summary>
    public bool Full;

    public void Clear() { Title = ""; Lines.Clear(); Progress = -1f; ProgressLabel = ""; Actions.Clear(); Steppers.Clear(); }

    /// <summary>A '- value +' row in the right-click panel (an Operation upgrade's level in use). A null OnMinus / OnPlus greys that button out.</summary>
    public struct DeviceStepper { public string Label, Value; public System.Action OnMinus, OnPlus; }

    /// <summary>The panel's stepper rows (shown under the lines).</summary>
    public readonly List<DeviceStepper> Steppers = new List<DeviceStepper>();

    /// <summary>Adds a '- value +' row to the right-click panel (call it when Full).</summary>
    public void AddStepper(string label, string value, System.Action onMinus, System.Action onPlus) => Steppers.Add(new DeviceStepper { Label = label, Value = value, OnMinus = onMinus, OnPlus = onPlus });

    /// <summary>A button of the device's own in the right-click panel.</summary>
    public struct DeviceAction { public string Label; public ButtonRole Role; public System.Action OnClick; }

    /// <summary>The device's own buttons (only used by the panel: add them when Full).</summary>
    public readonly List<DeviceAction> Actions = new List<DeviceAction>();

    /// <summary>Adds a button of the device's own to the right-click panel (call it when Full).</summary>
    public void AddAction(string label, System.Action onClick, ButtonRole role = ButtonRole.Primary) => Actions.Add(new DeviceAction { Label = label, Role = role, OnClick = onClick });

    /// <summary>A 'label: value' line.</summary>
    public void Add(string label, string value) => Lines.Add("<color=#9FB0CC>" + label + ":</color> <b>" + value + "</b>");

    /// <summary>A plain line (a hint, a state).</summary>
    public void Note(string text) => Lines.Add(text);

    /// <summary>A small grey heading line in the panel (e.g. 'This session').</summary>
    public void Heading(string text) => Lines.Add("<size=85%><color=#7F8CA6>" + text.ToUpperInvariant() + "</color></size>");

    /// <summary>Seconds as m:ss (h:mm:ss past an hour).</summary>
    public static string Clock(float seconds)
    {
        int s = UnityEngine.Mathf.Max(0, UnityEngine.Mathf.CeilToInt(seconds));
        return s >= 3600 ? (s / 3600) + ":" + (s / 60 % 60).ToString("00") + ":" + (s % 60).ToString("00") : (s / 60) + ":" + (s % 60).ToString("00");
    }
}
