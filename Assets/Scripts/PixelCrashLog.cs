using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Crash and error reports. While the game runs it keeps a text file (one per play session) in a "CrashReports" folder
/// next to the save. Every error or exception is written to it as it happens, with its stack trace, the last log lines
/// before it and a short summary of the game state (pixels, upgrades, minigames). A tester can simply send you those
/// .txt files.
///
/// It also notices when the previous session did not end normally (a crash, freeze or force-quit that no error message
/// could record) and tells the player, pointing at the folder. The pause menu's "Report Folder" button opens it.
///
/// Added automatically by PixelClicker. Add it to any GameObject yourself to change its settings in the Inspector.
/// </summary>
public class PixelCrashLog : MonoBehaviour
{
    [Header("Reports")]
    [Tooltip("Write the reports. Off = nothing is recorded.")]
    [SerializeField] private bool enableReports = true;

    [Tooltip("Also write warnings to the report (they are always kept in the 'recent log' lines). Off keeps the file small.")]
    [SerializeField] private bool writeWarnings = false;

    [Min(10)]
    [Tooltip("How many recent log lines are kept and written under each error, so you can see what happened just before it.")]
    [SerializeField] private int recentLines = 60;

    [Min(1)]
    [Tooltip("Old report files are deleted so no more than this many are kept (newest first).")]
    [SerializeField] private int keepFiles = 20;

    [Min(64)]
    [Tooltip("A report file stops growing past this size (kilobytes), so a repeating error can't fill the disk.")]
    [SerializeField] private int maxFileKilobytes = 2048;

    [Min(0f)]
    [Tooltip("The same error repeating within this many seconds is counted instead of written again.")]
    [SerializeField] private float repeatSeconds = 5f;

    [Header("Notice After a Crash")]
    [Tooltip("Show a box at startup if the last session did not end normally.")]
    [SerializeField] private bool showCrashNotice = true;

    [TextArea(2, 4)]
    [Tooltip("The notice. {0} = the folder the reports are in.")]
    [SerializeField] private string noticeText = "The game closed unexpectedly last time.\nA report was saved. Use Pause > Report Folder to find it and send the newest .txt file.";

    [Tooltip("Text on the button that closes the notice.")]
    [SerializeField] private string noticeOkText = "OK";

    [Min(0f)]
    [Tooltip("The notice closes by itself after this many seconds (0 = it stays until closed).")]
    [SerializeField] private float noticeSeconds = 20f;

    [Tooltip("Size of the notice box (canvas units).")]
    [SerializeField] private Vector2 noticeSize = new Vector2(900f, 230f);

    [Tooltip("Notice box colour.")]
    [SerializeField] private Color noticeColor = new Color(0.35f, 0.12f, 0.12f, 0.97f);

    [Tooltip("Sorting order of the notice's canvas (above the pause menu).")]
    [SerializeField] private int sortingOrder = 800;

    [Tooltip("Fallback font, used only when the PixelClicker's 'UI Font' is empty.")]
    [SerializeField] private TMP_FontAsset font;

    // ------------------------------------------------------------------

    private static PixelCrashLog instance;
    private readonly object gate = new object();
    private readonly Queue<string> recent = new Queue<string>();
    private string folder, sessionFile, markerFile;
    private string cachedSnapshot = "";
    private float snapshotTimer;
    private bool previousCrashed;
    private long written;
    private string lastKey = "";
    private double lastTime = -99d;
    private int repeatCount;
    private string previousFileName = "";
    private GameObject noticeRoot;
    private float noticeTimer;

    /// <summary>True when the report system is running.</summary>
    public static bool Available => instance != null && instance.enableReports;

    /// <summary>The folder the reports are written to.</summary>
    public static string FolderPath => instance != null ? instance.folder : "";

    /// <summary>Opens the report folder in the system's file browser.</summary>
    public static void OpenFolder()
    {
        if (instance == null || string.IsNullOrEmpty(instance.folder)) return;
        Directory.CreateDirectory(instance.folder);
#if UNITY_EDITOR
        UnityEditor.EditorUtility.RevealInFinder(instance.folder);
#else
        string path = instance.folder.Replace("\\", "/");
        Application.OpenURL((path.StartsWith("/") ? "file://" : "file:///") + path);
#endif
    }

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(this); return; }
        instance = this;
        if (!enableReports) return;

        try
        {
            folder = Path.Combine(Application.persistentDataPath, "CrashReports");
            Directory.CreateDirectory(folder);
            markerFile = Path.Combine(folder, "running.marker");

            // Did the last session end normally? (The marker is deleted on a clean quit.)
            if (File.Exists(markerFile))
            {
                previousCrashed = true;
                previousFileName = File.ReadAllText(markerFile).Trim();
            }

            DeleteOldFiles();
            sessionFile = Path.Combine(folder, "report_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".txt");
            File.WriteAllText(markerFile, Path.GetFileName(sessionFile));

            WriteHeader();
            Application.logMessageReceivedThreaded += OnLog;
        }
        catch (Exception e)
        {
            Debug.LogWarning("PixelCrashLog: couldn't set up the report folder (" + e.Message + ").");
            enableReports = false;
        }
    }

    private void Start()
    {
        if (!enableReports) return;
        RefreshSnapshot();
        if (previousCrashed && showCrashNotice) ShowNotice();
    }

    private void OnDestroy()
    {
        if (instance != this) return;
        Application.logMessageReceivedThreaded -= OnLog;
        instance = null;
    }

    private void OnApplicationQuit()
    {
        if (!enableReports || string.IsNullOrEmpty(markerFile)) return;
        try
        {
            FlushRepeats();
            Append("\n=== Session ended normally at " + Stamp() + " ===\n");
            if (File.Exists(markerFile)) File.Delete(markerFile); // a clean quit
        }
        catch (Exception) { /* nothing more can be done while quitting */ }
    }

    private void Update()
    {
        if (!enableReports) return;

        // The game-state summary is rebuilt on the main thread now and then; errors from any thread just reuse it.
        snapshotTimer -= Time.unscaledDeltaTime;
        if (snapshotTimer <= 0f)
        {
            snapshotTimer = 2f;
            RefreshSnapshot();
        }

        if (noticeRoot != null && noticeRoot.activeSelf && noticeSeconds > 0f)
        {
            noticeTimer -= Time.unscaledDeltaTime;
            if (noticeTimer <= 0f) noticeRoot.SetActive(false);
        }
    }

    // ------------------------------------------------------------------
    // Logging
    // ------------------------------------------------------------------

    private void OnLog(string condition, string stackTrace, LogType type)
    {
        if (!enableReports) return;

        string line = "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + type + ": " + condition;
        lock (gate)
        {
            recent.Enqueue(line);
            while (recent.Count > recentLines) recent.Dequeue();
        }

        bool isError = type == LogType.Error || type == LogType.Exception || type == LogType.Assert;
        if (!isError && !(writeWarnings && type == LogType.Warning)) return;

        try
        {
            lock (gate)
            {
                // The same error again and again is counted, not written each time.
                string key = type + "|" + condition + "|" + stackTrace;
                double now = DateTime.UtcNow.Ticks / (double)TimeSpan.TicksPerSecond; // Time.* may not be used off the main thread
                if (key == lastKey && now - lastTime < repeatSeconds)
                {
                    repeatCount++;
                    lastTime = now;
                    return;
                }
                FlushRepeats();
                lastKey = key;
                lastTime = now;

                StringBuilder sb = new StringBuilder();
                sb.AppendLine();
                sb.AppendLine("------------------------------------------------------------");
                sb.AppendLine(type.ToString().ToUpper() + " at " + Stamp());
                sb.AppendLine(condition);
                if (!string.IsNullOrEmpty(stackTrace)) sb.AppendLine(stackTrace.TrimEnd());
                sb.AppendLine();
                sb.AppendLine("Game state:");
                sb.AppendLine(cachedSnapshot);
                sb.AppendLine("Recent log:");
                foreach (string r in recent) sb.AppendLine("  " + r);
                Append(sb.ToString());
            }
        }
        catch (Exception) { /* a failing logger must never cause more errors */ }
    }

    private void FlushRepeats()
    {
        if (repeatCount <= 0) return;
        Append("(the error above repeated " + repeatCount + " more times)\n");
        repeatCount = 0;
    }

    private void Append(string text)
    {
        if (string.IsNullOrEmpty(sessionFile)) return;
        if (written > maxFileKilobytes * 1024L) return;
        File.AppendAllText(sessionFile, text);
        written += text.Length;
    }

    private static string Stamp() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

    private void DeleteOldFiles()
    {
        try
        {
            string[] files = Directory.GetFiles(folder, "report_*.txt");
            Array.Sort(files, StringComparer.Ordinal); // the names start with the date, so this is oldest first
            for (int i = 0; i < files.Length - (keepFiles - 1); i++) File.Delete(files[i]);
        }
        catch (Exception) { /* not important */ }
    }

    // ------------------------------------------------------------------
    // The header and the game-state summary
    // ------------------------------------------------------------------

    private void WriteHeader()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Pixel Clicker report - " + Stamp());
        sb.AppendLine("Game version: " + Application.version + "   Unity: " + Application.unityVersion);
        sb.AppendLine("Platform: " + Application.platform + "   OS: " + SystemInfo.operatingSystem);
        sb.AppendLine("Device: " + SystemInfo.deviceModel + "   CPU: " + SystemInfo.processorType + " x" + SystemInfo.processorCount);
        sb.AppendLine("Memory: " + SystemInfo.systemMemorySize + " MB   GPU: " + SystemInfo.graphicsDeviceName +
                      " (" + SystemInfo.graphicsMemorySize + " MB)");
        sb.AppendLine("Screen: " + Screen.width + "x" + Screen.height + "   Quality: " + QualitySettings.names[QualitySettings.GetQualityLevel()]);
        sb.AppendLine("Development build: " + Debug.isDebugBuild);
        if (previousCrashed)
            sb.AppendLine("NOTE: the previous session (" + (string.IsNullOrEmpty(previousFileName) ? "unknown file" : previousFileName) +
                          ") did not end normally - it may have crashed, frozen or been force-closed.");
        sb.AppendLine("Errors are listed below as they happen. If this file ends without a 'Session ended normally' line, the game closed unexpectedly.");
        Append(sb.ToString());
    }

    private void RefreshSnapshot()
    {
        try
        {
            StringBuilder sb = new StringBuilder();
            PixelClicker clicker = PixelFind.First<PixelClicker>();
            sb.AppendLine("  Time scale: " + Time.timeScale.ToString("0.##") + "   Paused: " + PixelPauseMenu.IsPaused +
                          "   Hose: " + PixelBank.HoseOn);

            PixelStats stats = PixelFind.First<PixelStats>();
            if (stats != null) sb.AppendLine("  Play time: " + (stats.PlaySeconds / 60d).ToString("0.#") + " min");

            if (clicker != null)
            {
                sb.Append("  Pixels:");
                foreach (PixelClicker.PixelTier t in clicker.Tiers)
                    if (t.unlocked) sb.Append(" ").Append(t.displayName).Append("=").Append(PixelClicker.FormatNumber(t.count));
                sb.AppendLine();
                sb.AppendLine("  Old pixels on the floor: " + clicker.OldPixels.Count + "   Ultra held: " + clicker.TotalUltra);
            }

            PixelShop shop = PixelFind.First<PixelShop>();
            if (shop != null)
            {
                sb.Append("  Bought:");
                for (int i = 0; i < shop.PackCount; i++)
                {
                    int level = shop.GetPackLevel(i);
                    if (shop.GetPackPurchased(i) || level > 0)
                        sb.Append(" ").Append(shop.GetPackName(i)).Append(level > 0 ? " L" + level : "").Append(";");
                }
                sb.AppendLine();
            }

            PixelConsumables consumables = PixelFind.First<PixelConsumables>();
            if (consumables != null)
                sb.AppendLine("  Active potion: " + (consumables.IsActive ? consumables.Get(consumables.ActiveIndex).displayName +
                              " (" + consumables.Remaining.ToString("0") + "s left)" : "none") +
                              "   Placing a device: " + consumables.IsPlacing +
                              "   Placed devices: " + PixelPlacedDevice.All.Count);

            sb.Append("  Minigames running:");
            foreach (PixelMinigame m in PixelMinigame.All) if (m != null && m.Running) sb.Append(" ").Append(m.Id).Append(";");
            sb.AppendLine();

            cachedSnapshot = sb.ToString().TrimEnd();
        }
        catch (Exception e)
        {
            cachedSnapshot = "  (couldn't read the game state: " + e.Message + ")";
        }
    }

    // ------------------------------------------------------------------
    // The notice
    // ------------------------------------------------------------------

    private void ShowNotice()
    {
        PixelClicker clicker = PixelFind.First<PixelClicker>();
        if (clicker != null && clicker.UIFont != null) font = clicker.UIFont;
        PixelUIKit.EnsureEventSystem();

        noticeRoot = PixelUIKit.CreateCanvas("Crash Notice Canvas", sortingOrder, new Vector2(1920f, 1080f), true);
        GameObject box = new GameObject("Crash Notice", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(noticeRoot.transform, false);
        box.GetComponent<Image>().color = noticeColor;
        RectTransform br = box.GetComponent<RectTransform>();
        br.anchorMin = br.anchorMax = br.pivot = new Vector2(0.5f, 0.5f);
        br.sizeDelta = noticeSize;
        br.anchoredPosition = Vector2.zero;

        TMP_Text label = PixelUIKit.CreateText(font, box.transform, "Text", string.Format(noticeText, folder), 32f,
                                               TextAlignmentOptions.Center, FontStyles.Normal, Color.white);
        RectTransform lr = label.rectTransform;
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = Vector2.one;
        lr.offsetMin = new Vector2(30f, 90f);
        lr.offsetMax = new Vector2(-30f, -20f);

        Button ok = PixelUIKit.CreateButton(font, box.transform, "Ok", noticeOkText, new Vector2(200f, 60f),
                                            new Color(0.5f, 0.2f, 0.2f, 1f), Color.white, 32f);
        RectTransform okr = ok.GetComponent<RectTransform>();
        okr.anchorMin = okr.anchorMax = okr.pivot = new Vector2(0.5f, 0f);
        okr.anchoredPosition = new Vector2(0f, 18f);
        ok.onClick.AddListener(() => noticeRoot.SetActive(false));

        noticeTimer = noticeSeconds;
    }
}
