// ---- System notes ----
// PixelBoundsSnapshot.cs
//   Dev tool for tuning where old pixels may go (the view bounds + the floor). Dev Tools > Tools > 'Snapshot bounds (3 s)' waits boundsSnapshotDelay
//   seconds (close the panel, set up the view you want) and then writes a text report to <persistentDataPath>/Snapshots/bounds_<time>.txt, copies it
//   to the clipboard and saves a screenshot next to it. The report holds: screen size, bars, the camera that is on screen AND the 'home' camera that
//   PixelViewBounds reads (pose, fov / ortho size, aspect, clip planes), the view kind / zoom / orbit state, the cube position and size, the floor
//   (y, mesh bounds, own ground settings), PixelViewBounds' settings and its five planes, physics numbers, where the viewport edges (corners and
//   edge middles, also inside the bars) land on the floor plane and on the cube's height, and every old pixel's position.
// ---------------------------------------------------
using System.Text;
using UnityEngine;

public static class PixelBoundsSnapshot
{
    /// <summary>Writes the report and the screenshot; returns the report file's path (null if it could not be written).</summary>
    public static string Capture()
    {
        PixelClicker clicker = PixelFind.First<PixelClicker>();
        if (clicker == null) return null;
        var sb = new StringBuilder(4096);
        Camera cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
        Camera home = PixelOrbitCamera.HomeCamera != null ? PixelOrbitCamera.HomeCamera : cam;
        PixelFloor floor = PixelFloor.Instance;
        float barFraction = 0f;
        if (PixelHud.Instance != null && PixelHud.Instance.RawBarHeight > 0f)
        {
            float scale = Mathf.Lerp(Screen.width / 1920f, Screen.height / 1080f, 0.5f);
            barFraction = Mathf.Clamp01(PixelHud.Instance.RawBarHeight * scale / Mathf.Max(1, Screen.height));
        }

        sb.AppendLine("Pixel Clicker bounds snapshot " + System.DateTime.UtcNow.ToString("u") + "  Unity " + Application.unityVersion);
        sb.AppendLine("screen=" + Screen.width + "x" + Screen.height + " aspect=" + ((float)Screen.width / Mathf.Max(1, Screen.height)).ToString("F4") + " fullscreen=" + Screen.fullScreen);
        sb.AppendLine("bars: RawBarHeight=" + (PixelHud.Instance != null ? PixelHud.Instance.RawBarHeight : 0f) + " BarHeight=" + (PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f) + " barFractionOfScreen=" + barFraction.ToString("F4") + " uiScale=" + PixelDisplaySettings.UIScale);
        sb.AppendLine();
        sb.AppendLine("== View ==");
        sb.AppendLine("view=" + PixelViewWindow.CurrentView + " orbitActive=" + PixelOrbitCamera.Active + " topDown=" + PixelOrbitCamera.TopDown + " firstPerson=" + PixelFirstPerson.Active +
                      " zoomed=" + PixelZoom.Zoomed + " zoomTarget=" + PixelZoom.Target.ToString("F3") + " orthoSetting=" + PixelOrthoView.Enabled);
        Describe(sb, "screen camera", cam);
        if (home != cam) Describe(sb, "home camera (what the bounds read)", home);
        sb.AppendLine();
        sb.AppendLine("== Cube ==");
        Vector3 basePos = clicker.CubeBasePosition;
        sb.AppendLine("cubeBase=" + basePos.ToString("F3") + " pixelBaseSize=" + clicker.PixelBaseSize.ToString("F3") + " oldPixelWorldSize=" + clicker.OldPixelWorldSize.ToString("F3") +
                      " gravity=" + Physics.gravity.ToString("F2") + " gravityScale=" + clicker.OldPixelGravityScale);
        sb.AppendLine();
        sb.AppendLine("== Floor ==");
        float floorY = basePos.y - 10f;
        if (floor != null && floor.HasFloor && floor.FloorRenderer != null)
        {
            Bounds b = floor.FloorRenderer.bounds;
            floorY = b.max.y;
            sb.AppendLine("floorObject=" + floor.FloorRenderer.name + " boundsCenter=" + b.center.ToString("F3") + " boundsSize=" + b.size.ToString("F3") + " topY=" + floorY.ToString("F3") +
                          " fallBelowCube=" + (basePos.y - floorY).ToString("F3"));
            Collider fc = floor.FloorRenderer.GetComponent<Collider>();
            if (fc != null) sb.AppendLine("floorCollider=" + fc.GetType().Name + " bounds=" + fc.bounds.center.ToString("F3") + " / " + fc.bounds.size.ToString("F3"));
        }
        else sb.AppendLine("no floor found (assumed y=" + floorY.ToString("F3") + ")");
        sb.AppendLine();
        sb.AppendLine("== PixelViewBounds ==");
        PixelViewBounds vb = PixelFind.First<PixelViewBounds>();
        if (vb != null) vb.Describe(sb); else sb.AppendLine("(component not found)");
        sb.AppendLine();
        if (home != null)
        {
            sb.AppendLine("== Where the home camera's viewport lands (x, z on the plane) ==");
            float[] xs = { 0f, 0.5f, 1f };
            float[] yBars = { 0f, barFraction, 0.5f, 1f - barFraction, 1f };
            sb.AppendLine("-- on the floor plane y=" + floorY.ToString("F3") + " (v = viewport height; bars at v=" + barFraction.ToString("F3") + " and " + (1f - barFraction).ToString("F3") + ")");
            foreach (float v in yBars) foreach (float u in xs) sb.AppendLine("  u=" + u.ToString("F2") + " v=" + v.ToString("F3") + " -> " + Hit(home, u, v, floorY));
            sb.AppendLine("-- on the cube's height y=" + basePos.y.ToString("F3"));
            foreach (float v in yBars) foreach (float u in xs) sb.AppendLine("  u=" + u.ToString("F2") + " v=" + v.ToString("F3") + " -> " + Hit(home, u, v, basePos.y));
            sb.AppendLine("cube on screen (home camera viewport) = " + home.WorldToViewportPoint(basePos).ToString("F3"));
        }
        sb.AppendLine();
        var pixels = clicker.OldPixels;
        sb.AppendLine("== Old pixels (" + pixels.Count + ") ==");
        Bounds all = new Bounds(basePos, Vector3.zero);
        int n = 0;
        for (int i = 0; i < pixels.Count; i++)
        {
            if (pixels[i] == null) continue;
            all.Encapsulate(pixels[i].position);
            if (n++ < 300) sb.AppendLine("  " + pixels[i].position.ToString("F2") + (pixels[i].isKinematic ? " kinematic" : ""));
        }
        sb.AppendLine("extent of all old pixels: min=" + all.min.ToString("F2") + " max=" + all.max.ToString("F2"));

        return PixelDevProbe.Save("bounds", sb.ToString(), true);
    }

    private static void Describe(StringBuilder sb, string label, Camera c)
    {
        if (c == null) { sb.AppendLine(label + ": none"); return; }
        Transform t = c.transform;
        sb.AppendLine(label + ": pos=" + t.position.ToString("F3") + " euler=" + t.eulerAngles.ToString("F2") + " forward=" + t.forward.ToString("F4") +
                      " ortho=" + c.orthographic + " fov=" + c.fieldOfView.ToString("F2") + " orthoSize=" + c.orthographicSize.ToString("F3") + " aspect=" + c.aspect.ToString("F4") +
                      " near=" + c.nearClipPlane + " far=" + c.farClipPlane);
    }

    private static string Hit(Camera c, float u, float v, float y)
    {
        Ray r = c.ViewportPointToRay(new Vector3(u, v, 0f));
        Plane p = new Plane(Vector3.up, new Vector3(0f, y, 0f));
        if (p.Raycast(r, out float d)) { Vector3 w = r.GetPoint(d); return "x=" + w.x.ToString("F2") + " z=" + w.z.ToString("F2"); }
        return "(ray never reaches this plane)";
    }
}
