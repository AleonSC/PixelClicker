// ---- System notes ----
// PixelStatusLines.cs
//   Keyed text lines for the top status box (PixelStatusBox, hanging under the potion / minigame status boxes): PixelStatusLines.Set(key, text, colour, seconds) shows
//   or replaces the line for that key (seconds > 0 = it goes away by itself, 0 = stays until Clear(key)); Clear(key) removes it. Several lines stack in one box
//   (first line's colour = the accent). The lines are cleared when a scene unloads (Restart reloads the scene; old lines must not come back). Used for the Black Hole counter, the Bomb timer / result, the Ghost / Meteor / Evil Tree messages that used to float in the world.
// ----------------------
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public static class PixelStatusLines
{
    private class Line { public string key, text; public Color color; public float expireAt; }

    private static readonly List<Line> lines = new List<Line>();
    private static Host host;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        lines.Clear(); host = null;
        // The list is static, so it outlives a scene reload (Settings > Restart reloads the scene): a line from the old game (the tree bar's 'Oak 3 / 9')
        // came back the next time anything set a line. Every scene change starts with no lines.
        UnityEngine.SceneManagement.SceneManager.sceneUnloaded -= OnSceneUnloaded;
        UnityEngine.SceneManagement.SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private static void OnSceneUnloaded(UnityEngine.SceneManagement.Scene scene) { lines.Clear(); host = null; }

    public static void Set(string key, string text, Color color, float seconds = 0f)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (host == null) host = Host.Make();
        Line l = lines.Find(x => x.key == key);
        if (l == null) { l = new Line { key = key }; lines.Add(l); }
        l.text = text; l.color = color;
        l.expireAt = seconds > 0f ? Time.unscaledTime + seconds : 0f;
    }

    public static void Clear(string key)
    {
        lines.RemoveAll(x => x.key == key);
    }

    private class Host : MonoBehaviour
    {
        private TMP_Text label;
        private readonly System.Text.StringBuilder sb = new System.Text.StringBuilder();

        public static Host Make()
        {
            GameObject canvas = PixelUIKit.CreateCanvas("Status Lines", 130, new Vector2(1920f, 1080f), false);
            PixelClicker clicker = PixelFind.First<PixelClicker>();
            Host h = canvas.AddComponent<Host>();
            h.label = PixelStatusBox.Create(clicker != null ? clicker.UIFont : null, canvas.transform, 40f, true);
            return h;
        }

        private void Update()
        {
            lines.RemoveAll(x => x.expireAt > 0f && Time.unscaledTime >= x.expireAt);
            if (label == null) return;
            if (lines.Count == 0) { PixelUIKit.SetText(label, ""); return; }
            sb.Length = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                if (i > 0) sb.Append('\n');
                Color32 c = lines[i].color;
                sb.Append("<color=#").Append(c.r.ToString("X2")).Append(c.g.ToString("X2")).Append(c.b.ToString("X2")).Append('>').Append(lines[i].text).Append("</color>");
            }
            PixelUIKit.SetText(label, sb.ToString());
            label.color = lines[0].color;
            PixelStatusBox.Show(label, true);
        }
    }
}
