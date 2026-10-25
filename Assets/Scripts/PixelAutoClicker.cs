// ---- System notes (moved here from CLAUDE.md) ----
// PixelAutoClicker.cs
//   Timer that calls clicker.AutoCollect() (paused while a takeover minigame runs or the cube is shrunk away:
//   PixelMinigame.TakeoverActive / PixelClicker.CubeHidden); Interval / ClicksPerTick properties; Activate() from shop; startRunning
//   for testing. PROGRESS (bought, switched off, upgraded interval / clicks) lives in PixelGameState.Current.autoClicker (GameState.cs,
//   moved 2026-10-23): saved / loaded / reset with it. intervalSeconds / clicksPerTick here are the BASE values (used while the state says 0).
// ---------------------------------------------------
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Clicks the pixel for the player at a fixed interval.
///
/// It does nothing until it is activated (the shop calls <see cref="Activate"/> when the
/// Auto Clicker pack is bought). Tick "Start Running" to test it without buying.
/// Each automatic click behaves like a real click (currency, effects, falling old pixel,
/// respawn) and its "+N" popup appears over the cube instead of at the mouse cursor.
/// </summary>
public class PixelAutoClicker : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The PixelClicker to click. Found automatically if left empty.")]
    [SerializeField] private PixelClicker clicker;

    [Header("Timing")]
    [Min(0.02f)]
    [Tooltip("Seconds between automatic clicks before any upgrade (the Interval upgrade's value is progress, kept in the GameState).")]
    [SerializeField] private float intervalSeconds = 1f;

    [Min(1)]
    [Tooltip("How many clicks happen each time the interval elapses, before any upgrade (the Clicks upgrade's value is kept in the GameState).")]
    [SerializeField] private int clicksPerTick = 1;

    [Tooltip("Keep clicking while the game is paused (Time.timeScale = 0).")]
    [SerializeField] private bool useUnscaledTime = false;

    [Tooltip("Safety limit: max clicks processed in one frame if the game hitches.")]
    [SerializeField] private int maxClicksPerFrame = 10;

    [Header("State")]
    [Tooltip("Run from the start without buying it (for testing). Whether it runs is progress: see the Pixel Game State component (State > Auto Clicker).")]
    [SerializeField] private bool startRunning = false;

    [Header("Events")]
    [Tooltip("Fired once when the auto clicker is first activated.")]
    public UnityEvent onActivated;

    [Tooltip("Fired for every automatic click.")]
    public UnityEvent onAutoClick;

    private float timer;

    // ---- progress: lives in the GameState (PixelGameState.Current.autoClicker) ----
    private static AutoClickerState S => PixelGameState.Current.autoClicker;

    public bool Running => S.running;

    /// <summary>The player switched the (bought) auto clicker off with the Toggles window. Saved.</summary>
    public bool UserDisabled { get => S.userDisabled; set => S.userDisabled = value; }

    /// <summary>Bought and not switched off: it is really clicking.</summary>
    public bool Working => S.running && !S.userDisabled;

    /// <summary>Seconds between automatic clicks (the upgraded value, or the Inspector base).</summary>
    public float Interval
    {
        get => S.interval > 0f ? S.interval : Mathf.Max(0.02f, intervalSeconds);
        set => S.interval = Mathf.Max(0.02f, value);
    }

    /// <summary>Clicks made each time the interval elapses (the upgraded value, or the Inspector base).</summary>
    public int ClicksPerTick
    {
        get => S.clicksPerTick > 0 ? S.clicksPerTick : Mathf.Max(1, clicksPerTick);
        set => S.clicksPerTick = Mathf.Max(1, value);
    }

    private void Awake()
    {
        if (clicker == null)
        {
            clicker = PixelFind.First<PixelClicker>();
        }

        if (clicker == null)
        {
            Debug.LogError("PixelAutoClicker: no PixelClicker found in the scene.", this);
            enabled = false;
            return;
        }

        if (startRunning) S.running = true;
    }

    private void Update()
    {
        if (!Working || PixelGameMode.TitleShowing) return;
        if (PixelMinigame.TakeoverActive || PixelMinigame.AutoClickerPaused || PixelWhaleFight.Active || (clicker != null && clicker.CubeHidden)) return; // nothing to click while an event hides the cube

        timer += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        float interval = Interval;
        int perTick = ClicksPerTick;
        int processed = 0;
        while (timer >= interval && processed < maxClicksPerFrame)
        {
            timer -= interval;
            processed++;

            for (int i = 0; i < perTick; i++)
            {
                clicker.AutoCollect();
                onAutoClick?.Invoke();
            }
        }

        // If we hit the per-frame cap, drop the backlog instead of bursting later.
        if (processed >= maxClicksPerFrame) timer = 0f;
    }

    /// <summary>Starts the auto clicker. Safe to call more than once.</summary>
    public void Activate()
    {
        if (S.running) return;
        S.running = true;
        timer = 0f;
        onActivated?.Invoke();
    }

    /// <summary>Stops the auto clicker.</summary>
    public void Deactivate() => S.running = false;
}

