using UnityEngine;

/// <summary>
/// Play statistics for Pixel Clicker: total clicks (manual and automatic), time played and pixels spent.
///
/// It just listens to PixelClicker, so nothing needs wiring. The numbers are saved by PixelSaveGame and shown
/// in the pause menu's Stats section. Add this to any GameObject (e.g. the cube); the pause menu adds one for you
/// if it is missing.
/// </summary>
public class PixelStats : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The PixelClicker to watch. Found automatically if left empty.")]
    [SerializeField] private PixelClicker clicker;

    [Header("Numbers (saved with the game - type values to test)")]
    [Tooltip("Clicks you made yourself. A click that only hits a tough pixel (Obsidian...) counts too.")]
    [SerializeField] private long manualClicks;

    [Tooltip("Clicks made by the auto clicker.")]
    [SerializeField] private long autoClicks;

    [Tooltip("Seconds spent playing (time while the game is paused doesn't count).")]
    [SerializeField] private double playSeconds;

    [Tooltip("Total pixels spent in the shop, of every type added together.")]
    [SerializeField] private double pixelsSpent;

    [Header("Behaviour")]
    [Tooltip("Keep counting play time while the pause menu is open.")]
    [SerializeField] private bool countTimeWhilePaused = false;

    public long ManualClicks => manualClicks;
    public long AutoClicks => autoClicks;
    public long TotalClicks => manualClicks + autoClicks;
    public double PlaySeconds => playSeconds;
    public double PixelsSpent => pixelsSpent;

    private void Start()
    {
        if (clicker == null)
        {
#if UNITY_2023_1_OR_NEWER
            clicker = FindFirstObjectByType<PixelClicker>();
#else
            clicker = FindObjectOfType<PixelClicker>();
#endif
        }
        if (clicker == null)
        {
            Debug.LogError("PixelStats: no PixelClicker found in the scene.", this);
            enabled = false;
            return;
        }

        clicker.PixelCollected += OnPixelCollected;
        clicker.PixelHit += OnPixelHit;
        clicker.CurrencySpent += OnCurrencySpent;
    }

    private void OnDestroy()
    {
        if (clicker == null) return;
        clicker.PixelCollected -= OnPixelCollected;
        clicker.PixelHit -= OnPixelHit;
        clicker.CurrencySpent -= OnCurrencySpent;
    }

    private void Update()
    {
        if (!countTimeWhilePaused && PixelPauseMenu.GameStopped) return;
        playSeconds += Time.unscaledDeltaTime;
    }

    private void OnPixelCollected(int tier, double amount, bool automatic) => CountClick(automatic);

    private void OnPixelHit(int tier, int hits, int needed, bool automatic) => CountClick(automatic);

    private void CountClick(bool automatic)
    {
        if (automatic) autoClicks++;
        else manualClicks++;
    }

    private void OnCurrencySpent(int tier, double amount) => pixelsSpent += amount;

    /// <summary>Restores the numbers from a save.</summary>
    public void SetState(long manual, long auto, double seconds, double spent)
    {
        manualClicks = System.Math.Max(0L, manual);
        autoClicks = System.Math.Max(0L, auto);
        playSeconds = System.Math.Max(0d, seconds);
        pixelsSpent = System.Math.Max(0d, spent);
    }

    /// <summary>Seconds as "1h 23m 45s" (hours and minutes are left out while they are zero).</summary>
    public static string FormatTime(double seconds)
    {
        long total = (long)System.Math.Max(0d, seconds);
        long h = total / 3600, m = total % 3600 / 60, s = total % 60;
        if (h > 0) return h + "h " + m + "m " + s + "s";
        if (m > 0) return m + "m " + s + "s";
        return s + "s";
    }
}
