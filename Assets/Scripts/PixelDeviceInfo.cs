// ---- System notes ----
// PixelDeviceInfo.cs
//   What a placed device tells the shared device card / panel (PixelDeviceCard). ONE system for every device: nothing builds a card of its own.
//   * PixelDeviceInfo: the filled-in sheet - Title, Lines (rich text, 'label: value'), an optional Progress 0..1 with its label, and Full (false =
//     the small hover card: only the essentials; true = the right-click panel: everything). Reused (Clear) every refresh.
//   * IPixelDeviceInfo: a component on a placed device's root that adds lines (FillDeviceInfo). PixelPlacedDevice.BuildInfo writes the common part
//     (name, time / uses left with the progress bar, 'Click to start', powered, attuned; Full: run time, reach, Operation levels), then its own
//     virtual AddInfo (subclasses: Storage Crate, Quantum Organizer...), then every IPixelDeviceInfo on the same object (the behaviour components
//     next to a thin device stub: PixelRecycler, PixelSortingHopper, PixelSweeper).
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

    public void Clear() { Title = ""; Lines.Clear(); Progress = -1f; ProgressLabel = ""; }

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
