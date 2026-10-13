// ---- System notes ----
// PixelDevProbe.cs
//   Dev snapshot tools that hand Claude what it cannot see (all driven by PixelDevTools: keys PixelAction.MarkMoment F10 / InspectHover F11 and
//   Tools tab buttons; Editor / development builds only). Reports go to <persistentDataPath>/Snapshots (Save) and the clipboard.
//   MarkMoment: screenshot + a text report of this instant (screen, view, camera, the crash log's game-state summary and last log lines).
//   UiLayoutDump: every active overlay canvas as a tree (name, rect in the 1920 x 1080 canvas space with the origin top-left like the mockup
//   builder, text + font size, colours, button / toggle / slider flags, raycast targets) - the UI is built in code, so this is the only way to see it as text.
//   Inspect: what is under the mouse right now - every UI object the pointer hits (topmost first, with its rect and look) and every collider along
//   the ray (name, scripts, old pixel tier / amount), plus where the cursor meets the floor plane. Copies to the clipboard and shows a short notice.
//   PixelBoundsSnapshot (the floor / view-bounds numbers) writes through the same Save.
// ---------------------------------------------------
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class PixelDevProbe
{
    private const float RefW = 1920f, RefH = 1080f, ToastSeconds = 2.5f;
    private const int MaxUiLines = 6000;

    /// <summary>Writes a report next to an optional screenshot, copies it to the clipboard and returns the file path (null if it failed).</summary>
    public static string Save(string prefix, string text, bool screenshot)
    {
        try
        {
            string dir = Path.Combine(Application.persistentDataPath, "Snapshots");
            Directory.CreateDirectory(dir);
            string stamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string path = Path.Combine(dir, prefix + "_" + stamp + ".txt");
            File.WriteAllText(path, text);
            if (screenshot) ScreenCapture.CaptureScreenshot(Path.Combine(dir, prefix + "_" + stamp + ".png"));
            GUIUtility.systemCopyBuffer = text;
            Debug.Log(prefix + " saved (and copied to the clipboard): " + path);
            return path;
        }
        catch (System.Exception e) { Debug.LogWarning(prefix + " failed: " + e.Message); return null; }
    }

    // ------------------------------------------------------------------
    // Mark this moment
    // ------------------------------------------------------------------

    /// <summary>Screenshot + state report of this instant. 'Something looked wrong right now.'</summary>
    public static string MarkMoment()
    {
        var sb = new StringBuilder(2048);
        sb.AppendLine("Pixel Clicker - marked moment " + System.DateTime.UtcNow.ToString("u") + "  Unity " + Application.unityVersion + "  build " + Application.version);
        sb.AppendLine("screen=" + Screen.width + "x" + Screen.height + " paused=" + PixelPauseMenu.IsPaused + " title=" + PixelTitleScreen.Showing + " timeScale=" + Time.timeScale +
                      " fps=" + (1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime)).ToString("F0"));
        sb.AppendLine("view=" + PixelViewWindow.CurrentView + " orbit=" + PixelOrbitCamera.Active + " zoomed=" + PixelZoom.Zoomed + " ortho=" + PixelOrthoView.Enabled);
        Camera cam = Camera.main;
        if (cam != null) sb.AppendLine("camera pos=" + cam.transform.position.ToString("F2") + " euler=" + cam.transform.eulerAngles.ToString("F1") + " fov=" + cam.fieldOfView.ToString("F1") + " ortho=" + cam.orthographic);
        sb.AppendLine("mouse=" + PixelInput.PointerPosition().ToString("F0"));
        sb.AppendLine();
        sb.AppendLine("Game state:");
        sb.AppendLine(PixelCrashLog.GameStateText());
        sb.AppendLine();
        sb.AppendLine("Recent log:");
        sb.AppendLine(PixelCrashLog.RecentLogText());
        return Save("moment", sb.ToString(), true);
    }

    // ------------------------------------------------------------------
    // UI layout dump
    // ------------------------------------------------------------------

    /// <summary>Every active overlay canvas as an indented tree with rects in the 1920 x 1080 canvas space (origin top-left).</summary>
    public static string UiLayoutDump()
    {
        var sb = new StringBuilder(16384);
        sb.AppendLine("Pixel Clicker - UI layout " + System.DateTime.UtcNow.ToString("u") + "  screen=" + Screen.width + "x" + Screen.height + "  UI scale=" + PixelDisplaySettings.UIScale);
        sb.AppendLine("Rects are x y w h in 1920 x 1080 canvas units, origin top-left (same space as the UI mockup builder). [r] = a raycast target (blocks clicks).");
        var canvases = new List<Canvas>();
        int skipped = 0;
        foreach (Canvas c in PixelFind.All<Canvas>())
        {
            if (!c.isRootCanvas || !c.gameObject.activeInHierarchy) continue;
            if (c.renderMode != RenderMode.ScreenSpaceOverlay) { skipped++; continue; }
            canvases.Add(c);
        }
        canvases.Sort((a, b) => a.sortingOrder.CompareTo(b.sortingOrder));
        int lines = 0;
        foreach (Canvas c in canvases)
        {
            sb.AppendLine();
            sb.AppendLine("== Canvas '" + c.name + "' sortingOrder=" + c.sortingOrder + " ==");
            Walk(c.transform, 0, sb, ref lines);
            if (lines >= MaxUiLines) { sb.AppendLine("(stopped: " + MaxUiLines + " lines)"); break; }
        }
        if (skipped > 0) sb.AppendLine("\n(" + skipped + " non-overlay canvases not listed)");
        return Save("ui", sb.ToString(), true);
    }

    private static void Walk(Transform parent, int depth, StringBuilder sb, ref int lines)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            if (lines >= MaxUiLines) return;
            Transform t = parent.GetChild(i);
            if (!t.gameObject.activeSelf) continue;
            RectTransform rt = t as RectTransform;
            if (rt != null)
            {
                sb.Append(' ', depth * 2).AppendLine(Describe(rt));
                lines++;
            }
            Walk(t, depth + 1, sb, ref lines);
        }
    }

    private static string RectText(RectTransform rt)
    {
        var corners = new Vector3[4];
        rt.GetWorldCorners(corners);   // overlay canvas: world = screen pixels, origin bottom-left
        float sw = Mathf.Max(1, Screen.width), sh = Mathf.Max(1, Screen.height);
        float x = corners[0].x / sw * RefW, w = (corners[2].x - corners[0].x) / sw * RefW;
        float y = (sh - corners[1].y) / sh * RefH, h = (corners[1].y - corners[0].y) / sh * RefH;
        return x.ToString("F0") + " " + y.ToString("F0") + " " + w.ToString("F0") + " " + h.ToString("F0");
    }

    /// <summary>One line: name, rect and what it draws / does.</summary>
    private static string Describe(RectTransform rt)
    {
        var sb = new StringBuilder(160);
        sb.Append(rt.name).Append("  [").Append(RectText(rt)).Append(']');
        TMP_Text tmp = rt.GetComponent<TMP_Text>();
        if (tmp != null)
        {
            string s = tmp.text == null ? "" : tmp.text.Replace("\n", "\\n");
            if (s.Length > 60) s = s.Substring(0, 60) + "...";
            sb.Append(" text=\"").Append(s).Append("\" size=").Append(tmp.fontSize.ToString("F0")).Append(" color=#").Append(ColorUtility.ToHtmlStringRGBA(tmp.color));
        }
        Image img = rt.GetComponent<Image>();
        if (img != null) sb.Append(" image color=#").Append(ColorUtility.ToHtmlStringRGBA(img.color)).Append(img.sprite != null ? " sprite=" + img.sprite.name : "");
        if (rt.GetComponent<RawImage>() != null) sb.Append(" rawimage");
        if (rt.GetComponent<Button>() != null) sb.Append(" BUTTON");
        if (rt.GetComponent<Toggle>() != null) sb.Append(" TOGGLE");
        if (rt.GetComponent<Slider>() != null) sb.Append(" SLIDER");
        if (rt.GetComponent<TMP_InputField>() != null) sb.Append(" INPUT");
        if (rt.GetComponent<TMP_Dropdown>() != null) sb.Append(" DROPDOWN");
        if (rt.GetComponent<ScrollRect>() != null) sb.Append(" SCROLL");
        if (rt.GetComponent<Mask>() != null || rt.GetComponent<RectMask2D>() != null) sb.Append(" mask");
        CanvasGroup cg = rt.GetComponent<CanvasGroup>();
        if (cg != null && cg.alpha < 0.999f) sb.Append(" alpha=").Append(cg.alpha.ToString("F2"));
        Graphic g = rt.GetComponent<Graphic>();
        if (g != null && g.raycastTarget) sb.Append(" [r]");
        return sb.ToString();
    }

    // ------------------------------------------------------------------
    // Hover inspector
    // ------------------------------------------------------------------

    /// <summary>What is under the mouse right now (UI first, then colliders), copied to the clipboard.</summary>
    public static void Inspect()
    {
        Vector2 pointer = PixelInput.PointerPosition();
        var sb = new StringBuilder(1024);
        sb.AppendLine("Pixel Clicker - under the mouse " + System.DateTime.UtcNow.ToString("u") + "  mouse=" + pointer.ToString("F0") + " (screen " + Screen.width + "x" + Screen.height + ")");
        string headline = "nothing";

        EventSystem es = EventSystem.current;
        if (es != null)
        {
            var hits = new List<RaycastResult>();
            es.RaycastAll(new PointerEventData(es) { position = pointer }, hits);
            sb.AppendLine();
            sb.AppendLine("UI under the mouse (topmost first): " + hits.Count);
            for (int i = 0; i < hits.Count; i++)
            {
                RectTransform rt = hits[i].gameObject.transform as RectTransform;
                if (rt == null) continue;
                sb.AppendLine("  " + (i + 1) + ". " + PathOf(rt) + "  canvas order " + (rt.GetComponentInParent<Canvas>() != null ? rt.GetComponentInParent<Canvas>().sortingOrder : 0));
                sb.AppendLine("       " + Describe(rt));
                if (i == 0) headline = "UI: " + rt.name;
            }
        }

        Camera cam = Camera.main;
        PixelClicker clicker = PixelFind.First<PixelClicker>();
        if (clicker != null && clicker.TargetCamera != null) cam = clicker.TargetCamera;
        if (cam != null)
        {
            Ray ray = cam.ScreenPointToRay(pointer);
            RaycastHit[] hits = Physics.RaycastAll(ray, 500f, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            sb.AppendLine();
            sb.AppendLine("World colliders along the ray (nearest first): " + hits.Length);
            for (int i = 0; i < hits.Length; i++)
            {
                Transform t = hits[i].collider.transform;
                sb.Append("  ").Append(i + 1).Append(". ").Append(PathOf(t)).Append("  dist=").Append(hits[i].distance.ToString("F2")).Append(" point=").Append(hits[i].point.ToString("F2"));
                if (hits[i].collider.isTrigger) sb.Append(" trigger");
                if (clicker != null && clicker.PixelTransform != null && t.IsChildOf(clicker.PixelTransform)) sb.Append(" (the cube)");
                OldPixelInfo info = t.GetComponentInParent<OldPixelInfo>();
                if (info != null && clicker != null && info.tierIndex >= 0 && info.tierIndex < clicker.Tiers.Length)
                    sb.Append(" OLD PIXEL tier=").Append(clicker.Tiers[info.tierIndex].displayName).Append(" amount=").Append(info.amount.ToString("0.##"));
                sb.Append(" scripts=");
                foreach (MonoBehaviour mb in t.GetComponents<MonoBehaviour>()) if (mb != null) sb.Append(mb.GetType().Name).Append(' ');
                sb.AppendLine();
                if (i == 0 && headline == "nothing") headline = "World: " + t.name;
            }
            float floorY = PixelFloor.Instance != null && PixelFloor.Instance.FloorRenderer != null ? PixelFloor.Instance.FloorRenderer.bounds.max.y : (clicker != null ? clicker.CubeBasePosition.y - 10f : 0f);
            if (new Plane(Vector3.up, new Vector3(0f, floorY, 0f)).Raycast(ray, out float d))
            {
                Vector3 p = ray.GetPoint(d);
                sb.AppendLine("\nCursor meets the floor plane (y=" + floorY.ToString("F2") + ") at x=" + p.x.ToString("F2") + " z=" + p.z.ToString("F2"));
            }
        }

        GUIUtility.systemCopyBuffer = sb.ToString();
        Debug.Log(sb.ToString());
        PixelNotice.Show("Copied: " + headline, ToastSeconds, null, true, true);
    }

    private static string PathOf(Transform t)
    {
        var parts = new List<string>();
        for (int n = 0; t != null && n < 6; n++, t = t.parent) parts.Add(t.name);
        parts.Reverse();
        return string.Join("/", parts);
    }
}
