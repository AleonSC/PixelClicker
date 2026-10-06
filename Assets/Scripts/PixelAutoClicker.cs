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
    [Tooltip("Seconds between automatic clicks.")]
    [SerializeField] private float intervalSeconds = 1f;

    [Min(1)]
    [Tooltip("How many clicks happen each time the interval elapses.")]
    [SerializeField] private int clicksPerTick = 1;

    [Tooltip("Keep clicking while the game is paused (Time.timeScale = 0).")]
    [SerializeField] private bool useUnscaledTime = false;

    [Tooltip("Safety limit: max clicks processed in one frame if the game hitches.")]
    [SerializeField] private int maxClicksPerFrame = 10;

    [Header("State")]
    [Tooltip("Run from the start without buying it (for testing).")]
    [SerializeField] private bool startRunning = false;

    [Tooltip("Is the auto clicker currently running? (Read-only in practice - changing it at runtime also works.)")]
    [SerializeField] private bool running = false;

    [Header("Events")]
    [Tooltip("Fired once when the auto clicker is first activated.")]
    public UnityEvent onActivated;

    [Tooltip("Fired for every automatic click.")]
    public UnityEvent onAutoClick;

    private float timer;

    public bool Running => running;

    /// <summary>The player switched the (bought) auto clicker off with the Toggles window. Saved.</summary>
    public bool UserDisabled { get; set; }

    /// <summary>Bought and not switched off: it is really clicking.</summary>
    public bool Working => running && !UserDisabled;

    /// <summary>Seconds between automatic clicks.</summary>
    public float Interval
    {
        get => intervalSeconds;
        set => intervalSeconds = Mathf.Max(0.02f, value);
    }

    /// <summary>Clicks made each time the interval elapses.</summary>
    public int ClicksPerTick
    {
        get => clicksPerTick;
        set => clicksPerTick = Mathf.Max(1, value);
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

        if (startRunning) running = true;
    }

    private void Update()
    {
        if (!running || UserDisabled) return;

        timer += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        int processed = 0;
        while (timer >= intervalSeconds && processed < maxClicksPerFrame)
        {
            timer -= intervalSeconds;
            processed++;

            for (int i = 0; i < clicksPerTick; i++)
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
        if (running) return;
        running = true;
        timer = 0f;
        onActivated?.Invoke();
    }

    /// <summary>Stops the auto clicker.</summary>
    public void Deactivate() => running = false;
}
