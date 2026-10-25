// ---- System notes ----
// PixelAutosaves.cs
//   Autosave history (for testing) + save screenshots. THREE parts in this file:
//   PixelAutosaves (static): every TIMER autosave (not manual saves, quit saves) is also copied into <persistentDataPath>/Autosaves as autosave_<ticks>.json + .meta
//   (when / total pixels / play time, so the list needs no big file read) + .jpg (the screenshot); PixelSaveGame.Save calls Add. At most PixelSaveGame.autosaveHistorySize (100) are
//   kept: Trim deletes the OLDEST when a newer one comes in. List() gives them newest first.
//   PixelSaveShots (static + a hidden runner): a screenshot of the game window (ScreenCapture, shrunk to 320 wide, JPG) next to EVERY save: slot saves go to <save file>.jpg, autosave
//   history to its own .jpg. A rolling copy is taken every few seconds while the game is visible and used when a save happens while paused (the Save / Load window shows the
//   pause menu, not the game). The rolling copy stays on the graphics card (CaptureScreenshotIntoRenderTexture, shrunk into a small RenderTexture): nothing waits for the GPU
//   and no JPG is made until a save needs the picture (it used to read back and encode the full screen every 2.5 s - a regular hitch). The capture comes out upside down when
//   SystemInfo.graphicsUVStartsAtTop (same rule as PixelFilters), so the shrink blit flips it there.
//   Load(path) returns a cached Texture2D for the hover tooltips (slot rows in the Save / Load window, rows of the autosave window).
//   PixelAutosaveWindow: the separate window opened by the small 'Autosaves' button in the Save / Load window (PixelPauseMenu.BuildSaveLoadPanel): a scrolling list (newest first)
//   with a Load button per row and a hover tooltip with the screenshot; Load = PixelSaveGame.LoadFile (does not change the current slot) and then resumes the game.
// ---------------------------------------------------
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class PixelAutosaves
{
    [Serializable]
    public class Meta
    {
        public string savedAt = "";
        public double totalPixels;
        public double playSeconds;
    }

    public struct Entry
    {
        public string jsonPath, thumbPath;
        public Meta meta;
    }

    public static string Folder => Path.Combine(Application.persistentDataPath, "Autosaves");

    /// <summary>Writes the autosave copy; returns the path its screenshot should be written to (the caller asks PixelSaveShots for it together with the slot's).</summary>
    public static string Add(string json, Meta meta, int keep)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            string stem = "autosave_" + DateTime.UtcNow.Ticks.ToString("D19");
            string path = Path.Combine(Folder, stem + ".json");
            File.WriteAllText(path, json);
            File.WriteAllText(Path.Combine(Folder, stem + ".meta"), JsonUtility.ToJson(meta));
            Trim(Mathf.Max(1, keep));
            return PixelSaveShots.ThumbPath(path);
        }
        catch (Exception e)
        {
            Debug.LogWarning("PixelAutosaves: could not keep the autosave - " + e.Message);
            return null;
        }
    }

    /// <summary>The newest autosave's file, or null (cheap: just the file names).</summary>
    public static string NewestPath()
    {
        try
        {
            if (!Directory.Exists(Folder)) return null;
            string best = null;
            foreach (string f in Directory.GetFiles(Folder, "autosave_*.json"))
                if (best == null || string.CompareOrdinal(f, best) > 0) best = f;
            return best;
        }
        catch (Exception) { return null; }
    }

    /// <summary>Deletes every autosave (Restart).</summary>
    public static void DeleteAll()
    {
        try
        {
            if (!Directory.Exists(Folder)) return;
            foreach (string f in Directory.GetFiles(Folder, "autosave_*")) { File.Delete(f); if (f.EndsWith(".jpg")) PixelSaveShots.Forget(f); }
        }
        catch (Exception) { }
    }

    /// <summary>The kept autosaves, newest first.</summary>
    public static List<Entry> List()
    {
        var list = new List<Entry>();
        try
        {
            if (!Directory.Exists(Folder)) return list;
            string[] files = Directory.GetFiles(Folder, "autosave_*.json");
            Array.Sort(files, StringComparer.Ordinal);
            for (int i = files.Length - 1; i >= 0; i--)
            {
                Entry e = new Entry { jsonPath = files[i], thumbPath = PixelSaveShots.ThumbPath(files[i]), meta = new Meta() };
                string metaPath = Path.ChangeExtension(files[i], ".meta");
                try { if (File.Exists(metaPath)) e.meta = JsonUtility.FromJson<Meta>(File.ReadAllText(metaPath)) ?? new Meta(); } catch (Exception) { }
                if (string.IsNullOrEmpty(e.meta.savedAt)) e.meta.savedAt = File.GetLastWriteTime(files[i]).ToString("yyyy-MM-dd HH:mm:ss");
                list.Add(e);
            }
        }
        catch (Exception) { }
        return list;
    }

    /// <summary>Deletes the oldest autosaves beyond 'keep' (their screenshots and info too).</summary>
    private static void Trim(int keep)
    {
        string[] files = Directory.GetFiles(Folder, "autosave_*.json");
        if (files.Length <= keep) return;
        Array.Sort(files, StringComparer.Ordinal);
        for (int i = 0; i < files.Length - keep; i++)
        {
            try
            {
                File.Delete(files[i]);
                string meta = Path.ChangeExtension(files[i], ".meta"), thumb = PixelSaveShots.ThumbPath(files[i]);
                if (File.Exists(meta)) File.Delete(meta);
                if (File.Exists(thumb)) File.Delete(thumb);
                PixelSaveShots.Forget(thumb);
            }
            catch (Exception) { }
        }
    }
}

public static class PixelSaveShots
{
    public const int ThumbWidth = 320;
    private const float CacheSeconds = 2.5f;

    private static RenderTexture screenCopy, thumb;   // the rolling copy: the whole screen, then shrunk to ThumbWidth (both live on the graphics card)
    private static bool haveThumb;                     // (thumb holds a picture of THIS game)
    private static byte[] thumbJpg;                    // thumb as a JPG, made the first time a save needs it (null = not made yet)
    private static ShotRunner runner;
    private static readonly Dictionary<string, KeyValuePair<long, Texture2D>> textures = new Dictionary<string, KeyValuePair<long, Texture2D>>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        screenCopy = thumb = null; haveThumb = false; thumbJpg = null; runner = null; textures.Clear();
        PixelStatics.ResetOnRestart(OnSceneUnloaded);   // restart-reset-not-progress: the save-screenshot thumbnail of this run
    }

    // (Restart reloads the scene: a save made while paused must not get the old game's last picture; the runner lives on, DontDestroyOnLoad)
    private static void OnSceneUnloaded() { haveThumb = false; thumbJpg = null; }

    // The rolling copy only runs once the runner exists; start it with the game, otherwise the first manual save (made from the pause menu) has no picture to use.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StartRunner()
    {
        if (runner == null) { runner = new GameObject("Save Shots").AddComponent<ShotRunner>(); UnityEngine.Object.DontDestroyOnLoad(runner.gameObject); }
    }

    /// <summary>The screenshot file that belongs to a save file.</summary>
    public static string ThumbPath(string savePath) => Path.ChangeExtension(savePath, ".jpg");

    /// <summary>A save was just written: take a screenshot of the game window and write it to these files (while the game is paused the rolling copy from just before is used).</summary>
    public static void Request(IList<string> jpgPaths)
    {
        if (jpgPaths == null || jpgPaths.Count == 0) return;
        if (runner == null) runner = new GameObject("Save Shots").AddComponent<ShotRunner>();
        runner.Take(jpgPaths);
    }

    /// <summary>The screenshot of a save as a texture (cached; null when there is none). Do not destroy it.</summary>
    public static Texture2D Load(string jpgPath)
    {
        try
        {
            if (string.IsNullOrEmpty(jpgPath) || !File.Exists(jpgPath)) return null;
            long stamp = File.GetLastWriteTimeUtc(jpgPath).Ticks;
            if (textures.TryGetValue(jpgPath, out KeyValuePair<long, Texture2D> hit) && hit.Key == stamp && hit.Value != null) return hit.Value;
            Forget(jpgPath);
            if (textures.Count > 40) { foreach (var kv in textures) if (kv.Value.Value != null) UnityEngine.Object.Destroy(kv.Value.Value); textures.Clear(); }
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGB24, false) { name = "SaveShot", filterMode = FilterMode.Bilinear };
            if (!tex.LoadImage(File.ReadAllBytes(jpgPath))) { UnityEngine.Object.Destroy(tex); return null; }
            textures[jpgPath] = new KeyValuePair<long, Texture2D>(stamp, tex);
            return tex;
        }
        catch (Exception) { return null; }
    }

    public static void Forget(string jpgPath)
    {
        if (jpgPath != null && textures.TryGetValue(jpgPath, out KeyValuePair<long, Texture2D> old))
        {
            if (old.Value != null) UnityEngine.Object.Destroy(old.Value);
            textures.Remove(jpgPath);
        }
    }

    private static void Write(IList<string> paths, byte[] jpg)
    {
        if (jpg == null) return;
        foreach (string p in paths)
        {
            if (string.IsNullOrEmpty(p)) continue;
            try { File.WriteAllBytes(p, jpg); Forget(p); } catch (Exception) { }
        }
    }

    private static bool GameVisible => !PixelCalm.InMenu && Screen.width > 8 && Screen.height > 8;

    /// <summary>Copies the screen into the small rolling picture, all on the graphics card. Call it at the end of a frame (after WaitForEndOfFrame).</summary>
    private static void CaptureRolling()
    {
        int sw = Screen.width, sh = Screen.height;
        if (screenCopy == null || screenCopy.width != sw || screenCopy.height != sh)
        {
            if (screenCopy != null) { screenCopy.Release(); UnityEngine.Object.Destroy(screenCopy); }
            screenCopy = new RenderTexture(sw, sh, 0, RenderTextureFormat.ARGB32) { name = "Save Shot Screen" };
            screenCopy.Create();
        }
        int w = ThumbWidth, h = Mathf.Max(1, Mathf.RoundToInt(w * (float)sh / Mathf.Max(1, sw)));
        if (thumb == null || thumb.width != w || thumb.height != h)
        {
            if (thumb != null) { thumb.Release(); UnityEngine.Object.Destroy(thumb); }
            thumb = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32) { name = "Save Shot Thumb", filterMode = FilterMode.Bilinear };
            thumb.Create();
        }
        ScreenCapture.CaptureScreenshotIntoRenderTexture(screenCopy);
        if (SystemInfo.graphicsUVStartsAtTop) Graphics.Blit(screenCopy, thumb, new Vector2(1f, -1f), new Vector2(0f, 1f));   // (the capture is upside down there)
        else Graphics.Blit(screenCopy, thumb);
        haveThumb = true;
        thumbJpg = null;
    }

    /// <summary>The rolling picture as a JPG (read back from the graphics card and encoded only now, once per picture). Null when there is none.</summary>
    private static byte[] ThumbJpg()
    {
        if (thumbJpg != null) return thumbJpg;
        if (!haveThumb || thumb == null || !thumb.IsCreated()) return null;
        Texture2D small = null;
        RenderTexture previous = RenderTexture.active;
        try
        {
            small = new Texture2D(thumb.width, thumb.height, TextureFormat.RGB24, false);
            RenderTexture.active = thumb;
            small.ReadPixels(new Rect(0, 0, thumb.width, thumb.height), 0, 0);
            small.Apply(false);
            thumbJpg = small.EncodeToJPG(75);
        }
        catch (Exception e) { Debug.LogWarning("PixelSaveShots: could not make the screenshot - " + e.Message); }
        finally
        {
            RenderTexture.active = previous;
            if (small != null) UnityEngine.Object.Destroy(small);
        }
        return thumbJpg;
    }

    private class ShotRunner : MonoBehaviour
    {
        private float timer;
        private bool busy;

        private void Update()
        {
            timer += Time.unscaledDeltaTime;
            if (timer >= CacheSeconds && !busy && GameVisible) { timer = 0f; StartCoroutine(Capture(null)); }
        }

        public void Take(IList<string> paths)
        {
            if (!GameVisible || busy) { Write(paths, ThumbJpg()); return; }   // paused (the Save / Load window is on screen): use the last picture of the game itself
            StartCoroutine(Capture(new List<string>(paths)));
        }

        /// <summary>Refreshes the rolling picture at the end of this frame; with 'paths' (a save was made) it is also written to those files.</summary>
        private IEnumerator Capture(IList<string> paths)
        {
            busy = true;
            yield return new WaitForEndOfFrame();
            try { CaptureRolling(); }
            catch (Exception e) { Debug.LogWarning("PixelSaveShots: could not take the screenshot - " + e.Message); }
            if (paths != null) Write(paths, ThumbJpg());   // (if the capture failed this is the previous picture, as before)
            busy = false;
        }
    }
}

/// <summary>The window with the list of autosaves (opened from the Save / Load window).</summary>
public class PixelAutosaveWindow : MonoBehaviour
{
    private static PixelAutosaveWindow instance;

    public static bool IsOpen => instance != null && instance.root != null && instance.root.activeSelf;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; }

    /// <summary>Opens the window. 'afterLoad' runs when an autosave was loaded (the pause menu resumes the game).</summary>
    public static void Open(PixelSaveGame save, Action afterLoad)
    {
        if (instance == null) instance = new GameObject("Autosave Window").AddComponent<PixelAutosaveWindow>();
        instance.Show(save, afterLoad);
    }

    /// <summary>Closes the window if it is open (true when it was). The pause menu calls it first on Escape.</summary>
    public static bool CloseIfOpen()
    {
        if (!IsOpen) return false;
        instance.root.SetActive(false);
        instance.tip.SetActive(false);
        return true;
    }

    private const float Width = 860f, Height = 800f, RowH = 76f, RowGap = 8f;
    private static readonly Color Back = new Color(0.1f, 0.12f, 0.18f, 0.98f), RowColor = new Color(0.2f, 0.24f, 0.34f, 1f);

    private PixelSaveGame save;
    private Action afterLoad;
    private PixelClicker clicker;
    private TMP_FontAsset font;
    private GameObject root, tip, listBar;
    private RectTransform listContent, tipImageRect;
    private RectTransform rootRect, tipRect;   // (the canvas's and the tooltip's RectTransforms, kept when they are built)
    private ScrollRect scroll;
    private TMP_Text statusLabel, emptyLabel, tipLabel;
    private RawImage tipImage;
    private List<PixelAutosaves.Entry> entries = new List<PixelAutosaves.Entry>();
    private int hover = -1;

    private void Awake()
    {
        clicker = PixelFind.First<PixelClicker>();
        font = clicker != null ? clicker.UIFont : null;
        Build();
    }

    private void Build()
    {
        PixelUIKit.EnsureEventSystem();
        root = PixelUIKit.CreateCanvas("Autosave Window Canvas", 520, new Vector2(1920f, 1080f), true);   // above the pause menu (500)
        root.transform.SetParent(transform, false);
        rootRect = root.GetComponent<RectTransform>();

        GameObject dim = new GameObject("Dim", typeof(RectTransform), typeof(Image));
        dim.transform.SetParent(root.transform, false);
        dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);   // also blocks the clicks behind the window
        PixelUIKit.Stretch(dim.GetComponent<RectTransform>());

        GameObject window = new GameObject("Window", typeof(RectTransform), typeof(Image));
        window.transform.SetParent(root.transform, false);
        PixelUIKit.StyleWindow(window.GetComponent<Image>(), Back);
        RectTransform wr = window.GetComponent<RectTransform>();
        wr.anchorMin = wr.anchorMax = wr.pivot = new Vector2(0.5f, 0.5f);
        wr.sizeDelta = new Vector2(Width, Height);

        TMP_Text title = PixelUIKit.CreateText(font, window.transform, "Title", "AUTOSAVES", 40f, TextAlignmentOptions.Center, FontStyles.Bold, Color.white);
        RectTransform tr = title.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f); tr.pivot = new Vector2(0.5f, 1f);
        tr.sizeDelta = new Vector2(0f, 56f);
        tr.anchoredPosition = new Vector2(0f, -22f);

        TMP_Text sub = PixelUIKit.CreateText(font, window.transform, "Sub", "Every autosave is kept here (for testing). Only the newest ones are kept: older ones are replaced. Hover a row for its screenshot.", 20f,
                                             TextAlignmentOptions.Center, FontStyles.Italic, new Color(0.75f, 0.8f, 0.9f, 1f));
        RectTransform sr = sub.rectTransform;
        sr.anchorMin = new Vector2(0f, 1f); sr.anchorMax = new Vector2(1f, 1f); sr.pivot = new Vector2(0.5f, 1f);
        sr.sizeDelta = new Vector2(-80f, 54f);
        sr.anchoredPosition = new Vector2(0f, -82f);

        Button close = PixelUIKit.CreateButton(font, window.transform, "Close", "X", new Vector2(56f, 56f), ButtonRole.Neutral, 28f);
        RectTransform cr = close.GetComponent<RectTransform>();
        cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(1f, 1f);
        cr.anchoredPosition = new Vector2(-18f, -18f);
        close.onClick.AddListener(() => CloseIfOpen());

        float listTop = 146f, listHeight = Height - listTop - 70f;
        scroll = PixelUIKit.CreateScrollView(window.transform, "List", new Color(0.5f, 0.55f, 0.65f, 1f), 12f, RowH + RowGap, out listContent, out listBar);
        RectTransform vr = scroll.GetComponent<RectTransform>();
        vr.anchorMin = new Vector2(0f, 1f); vr.anchorMax = new Vector2(1f, 1f); vr.pivot = new Vector2(0.5f, 1f);
        vr.sizeDelta = new Vector2(-60f, listHeight);
        vr.anchoredPosition = new Vector2(0f, -listTop);

        emptyLabel = PixelUIKit.CreateText(font, window.transform, "Empty", "No autosaves yet. One is added every time the game autosaves (Settings > Saving > Autosave).", 24f,
                                           TextAlignmentOptions.Center, FontStyles.Normal, new Color(0.8f, 0.85f, 0.95f, 1f));
        RectTransform er = emptyLabel.rectTransform;
        er.anchorMin = new Vector2(0.1f, 0.4f); er.anchorMax = new Vector2(0.9f, 0.6f);
        er.offsetMin = er.offsetMax = Vector2.zero;

        statusLabel = PixelUIKit.CreateText(font, window.transform, "Status", "", 24f, TextAlignmentOptions.Center, FontStyles.Bold, new Color(1f, 0.85f, 0.4f, 1f));
        RectTransform str = statusLabel.rectTransform;
        str.anchorMin = new Vector2(0f, 0f); str.anchorMax = new Vector2(1f, 0f); str.pivot = new Vector2(0.5f, 0f);
        str.sizeDelta = new Vector2(-60f, 50f);
        str.anchoredPosition = new Vector2(0f, 14f);

        // The hover tooltip: the screenshot above the details.
        tip = new GameObject("Tip", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        tip.transform.SetParent(root.transform, false);
        tipRect = tip.GetComponent<RectTransform>();
        tip.GetComponent<CanvasGroup>().blocksRaycasts = false;
        Image tbg = tip.GetComponent<Image>();
        tbg.color = new Color(0.07f, 0.09f, 0.13f, 0.97f);
        tbg.raycastTarget = false;
        PixelUIKit.StyleBox(tbg);
        GameObject img = new GameObject("Shot", typeof(RectTransform), typeof(RawImage));
        img.transform.SetParent(tip.transform, false);
        tipImage = img.GetComponent<RawImage>();
        tipImage.raycastTarget = false;
        tipImageRect = img.GetComponent<RectTransform>();
        tipImageRect.anchorMin = tipImageRect.anchorMax = tipImageRect.pivot = new Vector2(0.5f, 1f);
        tipLabel = PixelUIKit.CreateText(font, tip.transform, "Text", "", 22f, TextAlignmentOptions.TopLeft, FontStyles.Normal, Color.white);
        tipLabel.raycastTarget = false;
        tip.SetActive(false);
        root.SetActive(false);
    }

    private void Show(PixelSaveGame saveGame, Action loaded)
    {
        save = saveGame;
        afterLoad = loaded;
        statusLabel.text = "";
        Refresh();
        root.SetActive(true);
        PixelAudio.Play("menu_open");
        root.transform.SetAsLastSibling();
    }

    private static string PlayTime(double seconds)
    {
        int s = Mathf.Max(0, Mathf.RoundToInt((float)seconds));
        return s >= 3600 ? (s / 3600) + "h " + ((s % 3600) / 60).ToString("00") + "m" : (s / 60) + "m " + (s % 60).ToString("00") + "s";
    }

    private void Refresh()
    {
        entries = PixelAutosaves.List();
        for (int i = listContent.childCount - 1; i >= 0; i--) Destroy(listContent.GetChild(i).gameObject);
        emptyLabel.gameObject.SetActive(entries.Count == 0);
        float step = RowH + RowGap;
        for (int i = 0; i < entries.Count; i++)
        {
            int index = i;
            PixelAutosaves.Entry e = entries[i];
            GameObject row = new GameObject("Row " + (i + 1), typeof(RectTransform), typeof(Image), typeof(PixelHoverTip));
            row.transform.SetParent(listContent, false);
            row.GetComponent<Image>().color = RowColor;
            RectTransform rr = row.GetComponent<RectTransform>();
            rr.anchorMin = new Vector2(0f, 1f); rr.anchorMax = new Vector2(1f, 1f); rr.pivot = new Vector2(0.5f, 1f);
            rr.sizeDelta = new Vector2(-24f, RowH);
            rr.anchoredPosition = new Vector2(0f, -(i * step));
            PixelHoverTip h = row.GetComponent<PixelHoverTip>();
            h.onEnter = () => hover = index;
            h.onExit = () => { if (hover == index) hover = -1; };

            TMP_Text label = PixelUIKit.CreateText(font, row.transform, "Label", "<b>#" + (i + 1) + "</b>   " + e.meta.savedAt +
                                                   "\n<size=70%><color=#B8C4DA>" + PixelClicker.FormatNumberShort(e.meta.totalPixels) + " pixels collected   -   played " + PlayTime(e.meta.playSeconds) + "</color></size>",
                                                   26f, TextAlignmentOptions.MidlineLeft, FontStyles.Normal, Color.white);
            label.richText = true;
            label.raycastTarget = false;
            label.enableAutoSizing = true;
            label.fontSizeMax = 26f; label.fontSizeMin = 12f;
            label.overflowMode = TextOverflowModes.Truncate;
            PixelUIKit.Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(20f, 4f);
            label.rectTransform.offsetMax = new Vector2(-160f, -4f);

            Button load = PixelUIKit.CreateButton(font, row.transform, "Load", "Load", new Vector2(130f, RowH - 24f), ButtonRole.Primary, 26f);

            RectTransform lr = load.GetComponent<RectTransform>();
            lr.anchorMin = lr.anchorMax = lr.pivot = new Vector2(1f, 0.5f);
            lr.anchoredPosition = new Vector2(-12f, 0f);
            load.onClick.AddListener(() => LoadEntry(index));
        }
        float listHeight = scroll.GetComponent<RectTransform>().rect.height;
        PixelUIKit.UpdateScrollView(scroll, listBar, entries.Count * step, listHeight > 10f ? listHeight : Height - 216f);
    }

    private void LoadEntry(int index)
    {
        if (save == null || index < 0 || index >= entries.Count) return;
        PixelAutosaves.Entry e = entries[index];
        if (save.LoadFile(e.jsonPath))
        {
            CloseIfOpen();
            afterLoad?.Invoke();
            return;
        }
        statusLabel.text = "Could not load that autosave.";
    }

    private void Update()
    {
        if (root == null || !root.activeSelf) return;
        if (!PixelGameMode.PauseMenuOpen) { CloseIfOpen(); return; }   // the pause menu closed (a load resumed the game): the window goes too
        if (hover < 0 || hover >= entries.Count) { if (tip.activeSelf) tip.SetActive(false); return; }

        PixelAutosaves.Entry e = entries[hover];
        Texture2D shot = PixelSaveShots.Load(e.thumbPath);
        const float w = 520f;
        float imgH = 0f;
        tipImage.gameObject.SetActive(shot != null);
        if (shot != null)
        {
            tipImage.texture = shot;
            imgH = (w - 24f) * shot.height / Mathf.Max(1, shot.width);
            tipImageRect.sizeDelta = new Vector2(w - 24f, imgH);
            tipImageRect.anchoredPosition = new Vector2(0f, -12f);
        }
        string text = "<b>" + e.meta.savedAt + "</b>\n" + PixelClicker.FormatNumberShort(e.meta.totalPixels) + " pixels collected\nPlayed " + PlayTime(e.meta.playSeconds) + (shot == null ? "\n<i>(no screenshot for this one)</i>" : "");
        if (tipLabel.text != text) tipLabel.text = text;
        RectTransform lr = tipLabel.rectTransform;
        lr.anchorMin = new Vector2(0f, 1f); lr.anchorMax = new Vector2(1f, 1f); lr.pivot = new Vector2(0.5f, 1f);
        float textH = Mathf.Ceil(tipLabel.GetPreferredValues(text, w - 32f, 0f).y);
        lr.sizeDelta = new Vector2(-32f, textH);
        lr.anchoredPosition = new Vector2(0f, -(imgH + (shot != null ? 24f : 12f)));
        RectTransform rt = tipRect;
        rt.sizeDelta = new Vector2(w, imgH + textH + (shot != null ? 36f : 24f));
        if (!tip.activeSelf) tip.SetActive(true);
        tip.transform.SetAsLastSibling();

        RectTransform canvasRect = rootRect;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, PixelInput.PointerPosition(), null, out Vector2 local)) return;
        bool rightHalf = local.x > 0f;
        rt.pivot = new Vector2(rightHalf ? 1f : 0f, local.y < -canvasRect.rect.height * 0.25f ? 0f : 1f);
        rt.anchoredPosition = local + new Vector2(rightHalf ? -18f : 18f, rt.pivot.y > 0.5f ? -22f : 22f);
    }
}
