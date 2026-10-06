using System;
using System.Collections.Generic;
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

    [Header("Minigames, devices and combo (saved with the game)")]
    [Tooltip("Ghosts you clicked.")]
    [SerializeField] private long ghostsClicked;

    [Tooltip("Meteor clicks (every click on a meteor counts).")]
    [SerializeField] private long meteorsClicked;

    [Tooltip("Meteors that appeared.")]
    [SerializeField] private long meteorsSpawned;

    [Tooltip("Black holes that opened.")]
    [SerializeField] private long blackHolesSpawned;

    [Tooltip("Fans you placed.")]
    [SerializeField] private long fansUsed;

    [Tooltip("Vacuum devices you placed.")]
    [SerializeField] private long vacuumDevicesUsed;

    [Tooltip("Highest combo you reached.")]
    [SerializeField] private int highestCombo;

    [Header("Behaviour")]
    [Tooltip("Keep counting play time while the pause menu is open.")]
    [SerializeField] private bool countTimeWhilePaused = false;

    public long ManualClicks => manualClicks;
    public long AutoClicks => autoClicks;
    public long TotalClicks => manualClicks + autoClicks;
    public double PlaySeconds => playSeconds;
    public double PixelsSpent => pixelsSpent;
    public long GhostsClicked => ghostsClicked;
    public long MeteorsClicked => meteorsClicked;
    public long MeteorsSpawned => meteorsSpawned;
    public long BlackHolesSpawned => blackHolesSpawned;
    public long FansUsed => fansUsed;
    public long VacuumDevicesUsed => vacuumDevicesUsed;
    public int HighestCombo => highestCombo;

    /// <summary>How many of each potion you drank (name, count), in the order first used.</summary>
    public IReadOnlyList<KeyValuePair<string, long>> PotionsUsed => potionsUsed;

    /// <summary>Total potions drunk.</summary>
    public long TotalPotionsUsed
    {
        get
        {
            long total = 0;
            foreach (KeyValuePair<string, long> p in potionsUsed) total += p.Value;
            return total;
        }
    }

    private readonly List<KeyValuePair<string, long>> potionsUsed = new List<KeyValuePair<string, long>>();

    /// <summary>The extra stats in a form the save file can store.</summary>
    [Serializable]
    public class ExtraData
    {
        public long ghostsClicked, meteorsClicked, meteorsSpawned, blackHolesSpawned, fansUsed, vacuumDevicesUsed;
        public int highestCombo;
        public string[] potionNames;
        public long[] potionCounts;
    }

    private void Start()
    {
        if (clicker == null)
        {
            clicker = PixelFind.First<PixelClicker>();
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

        PixelMinigame.Happened += OnMinigame;
        PixelConsumables.PotionDrunk += OnPotionDrunk;
        PixelConsumables.DevicePlaced += OnDevicePlaced;
        PixelCombo.ComboReached += OnCombo;
    }

    private void OnDestroy()
    {
        PixelMinigame.Happened -= OnMinigame;
        PixelConsumables.PotionDrunk -= OnPotionDrunk;
        PixelConsumables.DevicePlaced -= OnDevicePlaced;
        PixelCombo.ComboReached -= OnCombo;
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

    private void OnMinigame(PixelMinigame game, PixelMinigame.MinigameEvent what)
    {
        bool clicked = what == PixelMinigame.MinigameEvent.Clicked;
        switch (game.Id)
        {
            case "ghost": if (clicked) ghostsClicked++; break;
            case "meteor": if (clicked) meteorsClicked++; else meteorsSpawned++; break;
            case "blackhole": if (!clicked) blackHolesSpawned++; break;
        }
    }

    private void OnPotionDrunk(string potionName)
    {
        int i = potionsUsed.FindIndex(p => p.Key == potionName);
        if (i >= 0) potionsUsed[i] = new KeyValuePair<string, long>(potionName, potionsUsed[i].Value + 1);
        else potionsUsed.Add(new KeyValuePair<string, long>(potionName, 1));
    }

    private void OnDevicePlaced(PixelConsumables.DeviceKind kind)
    {
        if (kind == PixelConsumables.DeviceKind.Fan) fansUsed++;
        else vacuumDevicesUsed++;
    }

    private void OnCombo(int count)
    {
        if (count > highestCombo) highestCombo = count;
    }

    /// <summary>The extra stats for saving.</summary>
    public ExtraData GetExtra()
    {
        ExtraData d = new ExtraData
        {
            ghostsClicked = ghostsClicked, meteorsClicked = meteorsClicked, meteorsSpawned = meteorsSpawned,
            blackHolesSpawned = blackHolesSpawned, fansUsed = fansUsed, vacuumDevicesUsed = vacuumDevicesUsed,
            highestCombo = highestCombo,
            potionNames = new string[potionsUsed.Count],
            potionCounts = new long[potionsUsed.Count],
        };
        for (int i = 0; i < potionsUsed.Count; i++)
        {
            d.potionNames[i] = potionsUsed[i].Key;
            d.potionCounts[i] = potionsUsed[i].Value;
        }
        return d;
    }

    /// <summary>Restores the extra stats from a save (null = an older save without them).</summary>
    public void SetExtra(ExtraData d)
    {
        potionsUsed.Clear();
        if (d == null)
        {
            ghostsClicked = meteorsClicked = meteorsSpawned = blackHolesSpawned = fansUsed = vacuumDevicesUsed = 0;
            highestCombo = 0;
            return;
        }
        ghostsClicked = Math.Max(0L, d.ghostsClicked);
        meteorsClicked = Math.Max(0L, d.meteorsClicked);
        meteorsSpawned = Math.Max(0L, d.meteorsSpawned);
        blackHolesSpawned = Math.Max(0L, d.blackHolesSpawned);
        fansUsed = Math.Max(0L, d.fansUsed);
        vacuumDevicesUsed = Math.Max(0L, d.vacuumDevicesUsed);
        highestCombo = Math.Max(0, d.highestCombo);
        if (d.potionNames == null || d.potionCounts == null) return;
        for (int i = 0; i < d.potionNames.Length && i < d.potionCounts.Length; i++)
            potionsUsed.Add(new KeyValuePair<string, long>(d.potionNames[i], Math.Max(0L, d.potionCounts[i])));
    }

    /// <summary>Adds automatic clicks that happened while the game was closed (offline progress).</summary>
    public void AddAutoClicks(long count)
    {
        if (count > 0) autoClicks += count;
    }

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
