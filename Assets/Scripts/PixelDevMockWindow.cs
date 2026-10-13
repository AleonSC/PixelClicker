// ---- System notes ----
// PixelDevMockWindow.cs
//   Just a doorway now. The Mock up mode WINDOW is gone: the Shape placer (PixelDevShapes) and the UI mockup builder (PixelDevMockup) are tabs of the Dev Hub (PixelDevHub: 'Shapes' and
//   'UI Mock'). They still ask this class for their content roots (TabRoot 0 = Shapes, 1 = UI pieces) and for the height they may use (ContentHeight); the hub tells them when their tab is
//   on screen (PixelDevShapes.SetActive / PixelDevMockup.SetActive) and whether the hub is open (PixelDevMockup.SetWindowOpen).
// ---------------------------------------------------
using UnityEngine;

public static class PixelDevMockWindow
{
    public const float Width = 500f, ContentHeight = PixelDevHub.ContentH;

    public static void Boot() { PixelDevHub.Boot(); }

    /// <summary>The root a tool's controls are built into (0 = Shape placer, 1 = UI mockup): 'Width' wide, 'ContentHeight' tall, origin top-left.</summary>
    public static RectTransform TabRoot(int tab) { return PixelDevHub.TabRoot(tab == 0 ? "shapes" : "uimock"); }
}
