// ---- System notes ----
// PixelConveyorPanel.cs
//   The BELT PANEL (2026-10-20, Devices session): a quick RIGHT CLICK on a conveyor belt (normal view; PixelConveyorSystem) opens the shared device
//   panel (PixelDeviceCard.OpenSubject - the same window as every placed device) on that belt's SECTION: the belts a Control Lever would switch with it
//   (PixelConveyor.CollectSection - the whole joined line, or the part between two Belt Sorter joints). While the panel is open the section GLOWS
//   (PixelConveyor.SetHighlight), so you see what you are editing.
//   The sheet: how many pieces and how long, the speed, the lane count (Electrician), husks riding, stopped by a lever or not, and a STATUS line
//   (PixelConveyor.EndStatus: running, feeding a machine, waiting for a full / busy machine, or spilling and why - nothing attached, another height, head-on). A SPEED stepper
//   (- / +, speedStep cube widths a second, the whole section), a SIZE stepper (lanes 1..the Electrician maximum: PixelConveyor.SetLanes), actions REVERSE (every piece of the section turns round) and REMOVE THIS PIECE (hovering it makes only that piece glow, red), and the
//   panel's own Remove button takes the WHOLE SECTION away (asks once). A right HOLD on a belt still removes just that piece (PixelFeatureTester).
//   Not a MonoBehaviour: one small object per opened panel (the settings below are constants for now).
// ---------------------------------------------------
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public sealed class PixelConveyorPanel : IPixelPanelSubject
{
    private const float SpeedStep = 0.2f, MinSpeed = 0.2f, MaxSpeed = 8f;

    private readonly PixelConveyor clicked;
    private readonly List<PixelConveyor> section = new List<PixelConveyor>();
    private readonly List<PixelConveyor> lit = new List<PixelConveyor>();
    private bool pieceHover;   // the pointer is on 'Remove this piece': only that piece glows, red

    private PixelConveyorPanel(PixelConveyor belt) { clicked = belt; }

    /// <summary>Opens the panel on this belt's section.</summary>
    public static void Open(PixelConveyor belt)
    {
        if (belt == null || belt.IsGhost) return;
        PixelDeviceCard.OpenSubject(new PixelConveyorPanel(belt));
    }

    /// <summary>The section again (pieces may have been added, split or removed), with the glow moved onto it.</summary>
    private void Refresh()
    {
        if (clicked != null) clicked.CollectSection(section);
        else
        {
            section.RemoveAll(b => b == null);
            if (section.Count > 0) { PixelConveyor any = section[0]; any.CollectSection(section); }
        }
        foreach (PixelConveyor b in lit) if (b != null && !section.Contains(b)) b.SetHighlight(false);
        foreach (PixelConveyor b in section)
        {
            if (b == null) continue;
            if (!pieceHover) b.SetHighlight(true);
            else if (b == clicked) b.SetHighlight(true, true);
            else b.SetHighlight(false);
        }
        lit.Clear();
        lit.AddRange(section);
    }

    // ---- IPixelPanelSubject ----

    public bool PanelAlive { get { section.RemoveAll(b => b == null); return clicked != null || section.Count > 0; } }

    public void PanelOpened() { Refresh(); PixelAudio.Play("ui_click", 1.1f); }

    public void PanelClosed()
    {
        pieceHover = false;
        foreach (PixelConveyor b in lit) if (b != null) b.SetHighlight(false);
        lit.Clear();
    }

    public void BuildPanelInfo(PixelDeviceInfo info)
    {
        Refresh();
        int pieces = section.Count;
        info.Title = pieces > 1 ? "Conveyor Belt Section" : "Conveyor Belt";
        PixelConveyor first = section.Count > 0 ? section[0] : null;
        if (first == null) return;
        float length = 0f, unit = Mathf.Max(0.05f, first.Unit);
        bool halted = false;
        foreach (PixelConveyor b in section) { length += b.Length; halted |= b.Halted; }
        float speed = first.SpeedCubes;
        bool mixed = false;
        foreach (PixelConveyor b in section) if (Mathf.Abs(b.SpeedCubes - speed) > 0.01f) mixed = true;
        int riders = 0;
        foreach (PixelConveyor b in section) riders += PixelConveyorSystem.RidersOn(b);

        info.Add("Pieces", pieces.ToString());
        info.Add("Length", (length / unit).ToString("0.#", CultureInfo.InvariantCulture) + " cubes");
        info.Add("Speed", (mixed ? "mixed, " : "") + speed.ToString("0.0#", CultureInfo.InvariantCulture) + " cubes / s");
        int lanes = first.Lanes, max = PixelConveyor.LaneCount;
        bool mixedLanes = false;
        foreach (PixelConveyor b in section) if (b.Lanes != lanes) mixedLanes = true;
        info.Add("Size", (mixedLanes ? "mixed, " : "") + lanes + (lanes == 1 ? " lane" : " lanes") + " (up to " + max + ")");
        if (max < 3) info.Note("<color=#9FB0CC>Electrician level " + (max < 2 ? 10 : 20) + " allows " + (max + 1) + " lanes.</color>");
        info.Add("Husks riding", riders.ToString());
        // (2026-10-20) What happens at the section's end(s), in words: running on, feeding a machine, waiting for a full one, or spilling and why.
        string status = null;
        foreach (PixelConveyor b in section)
        {
            string s = b != null ? b.EndStatus() : null;
            if (s == null) continue;
            if (status == null) status = s;
            else if (s != status) info.Note("<color=#9FB0CC>Another end: " + s + "</color>");
        }
        info.Add("Status", status ?? "Running");
        if (halted) info.Note("<color=#FF8A7A>Stopped by a Control Lever - flip it to run again.</color>");
        info.Note("<color=#9FB0CC>The glowing belts are this section: everything here applies to all of them.</color>");

        string shown = speed.ToString("0.0#", CultureInfo.InvariantCulture) + " cubes / s";
        info.AddStepper("Speed", shown,
                        speed > MinSpeed + 0.001f ? () => SetSpeed(Mathf.Max(MinSpeed, Round(speed - SpeedStep))) : (System.Action)null,
                        speed < MaxSpeed - 0.001f ? () => SetSpeed(Mathf.Min(MaxSpeed, Round(speed + SpeedStep))) : (System.Action)null);
        info.AddStepper("Size", lanes + (lanes == 1 ? " lane" : " lanes"),
                        lanes > 1 ? () => SetLanes(lanes - 1) : (System.Action)null,
                        lanes < max ? () => SetLanes(lanes + 1) : (System.Action)null);
        info.AddAction("Reverse", Reverse, ButtonRole.Primary);
        if (pieces > 1 && clicked != null) info.AddAction("Remove this piece", RemovePiece, ButtonRole.Danger, HoverRemovePiece);
    }

    private static float Round(float v) => Mathf.Round(v / SpeedStep) * SpeedStep;

    private void SetSpeed(float cubesPerSecond)
    {
        foreach (PixelConveyor b in section) if (b != null) b.SetSpeedCubes(cubesPerSecond);
        PixelAudio.Play("ui_click", 1.2f);
    }

    /// <summary>Every belt of the section is built again at this width (lanes).</summary>
    private void SetLanes(int lanes)
    {
        foreach (PixelConveyor b in section) if (b != null) { b.SetHighlight(false); b.SetLanes(lanes); }
        lit.Clear();   // (the glow is put back on the rebuilt belts by the panel's refresh)
        PixelAudio.Play("device_place", 1.2f);
    }

    /// <summary>Every piece of the section turns round (the whole section runs the other way).</summary>
    private void Reverse()
    {
        foreach (PixelConveyor b in section) if (b != null) b.Flip();
    }

    /// <summary>The pointer on 'Remove this piece': only the clicked piece glows (red) so you see which one goes; off it, the whole section glows again.</summary>
    private void HoverRemovePiece(bool on)
    {
        if (pieceHover == on) return;
        pieceHover = on;
        Refresh();
    }

    private void RemovePiece()
    {
        pieceHover = false;
        if (clicked == null) return;
        clicked.SetHighlight(false);
        PixelFeatureTester.RemovePlaced(clicked.gameObject);
        PixelAudio.Play("device_remove");
    }

    /// <summary>The panel's Remove: the whole section goes.</summary>
    public void PanelRemove()
    {
        PanelClosed();
        foreach (PixelConveyor b in section) if (b != null) PixelFeatureTester.RemovePlaced(b.gameObject);
        section.Clear();
        PixelAudio.Play("device_remove");
    }
}
