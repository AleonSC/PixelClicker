// ---- System notes ----
// PixelCubeScreenSpot.cs
//   Puts the clickable cube at a spot on the screen (a viewport point, 0..1) at the same distance from the camera as its home
//   spot, so it keeps its size: PixelClicker.SetCubeOffset(world - CubeBasePosition). Used by the cube minigames that move the
//   cube around the screen (Cube Blink, Cube Dash, Cube Evasion) and the Whale fight (PixelWhaleFight).
//   Why: the same four lines sat in each of them (Code Health 'duplicate' round, 2026-10-24).
// ---------------------------------------------------
using UnityEngine;

/// <summary>Moves the clickable cube to a spot on the screen, keeping its distance from the camera (and so its size).</summary>
public static class PixelCubeScreenSpot
{
    /// <summary>Offsets the cube so it shows at this viewport point (0..1 on both axes) of the camera.</summary>
    public static void Place(PixelClicker clicker, Camera cam, Vector2 viewport)
    {
        Vector3 basePos = clicker.CubeBasePosition;
        float depth = Vector3.Dot(basePos - cam.transform.position, cam.transform.forward);
        Vector3 world = cam.ViewportToWorldPoint(new Vector3(viewport.x, viewport.y, depth));
        clicker.SetCubeOffset(world - basePos);
    }
}
