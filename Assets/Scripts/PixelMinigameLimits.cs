// ---- System notes (moved here from CLAUDE.md) ----
// PixelMinigameLimits.cs
//   Limits how many minigames have something on screen together (minAtOnce 2 / maxAtOnce 3, limitAtOnce, quietSecondsAfterPlay 30
//   (nothing spawns that soon after Play; PixelTitleScreen.PlayTime), retrySeconds; added by PixelClicker.Awake). Each minigame
//   overrides PixelMinigame.Busy and asks PixelMinigameLimits.AllowAnother(this) before its timer spawns something (by default only
//   ONE minigame / visitor may be busy at a time; the allowOverlap tick box brings back the min..max numbers, a number between them is
//   rolled each time); SpawnNow (dev tools) ignores the limit. A new minigame must do the same. Minigames that take over the floor's
//   old pixels (Snake, Sorting Race) override PixelMinigame.Takeover and set PixelMinigame.TakeoverActive; AllowAnother refuses every
//   minigame (takeover or not, and the Stardust stars) while a takeover round is Busy, and Pixel Grabbing / cube clicks are off
//   meanwhile. The Ultra Pad and Black Hole never spawn on top of each other: PixelMinigame.ClaimArea / AreaFree keep a list of the
//   round floor areas in use, and each one's TryPickSpot rejects spots that overlap the other's.
//   BREATHERS: when the last busy minigame / visit finishes, Update starts a breather (breatherMin 40 .. Max 90 s at random, x takeoverBreatherFactor 1.5 after a takeover round) during which AllowAnother
//   says no, so minigames bought together no longer follow one another straight away; RetrySeconds is randomised so waiting minigames do not all poll on the same frame (disableBreathers switches it off).
// ---------------------------------------------------
using UnityEngine;

/// <summary>
/// How many minigames may have something on screen at the same time (a ghost, a meteor, a black hole, a bomb, a pad).
/// Each time a minigame is due to spawn, it only does so if fewer than a random number between Min At Once and Max At Once
/// are already busy; otherwise it tries again a few seconds later. Added automatically; add it yourself to change the numbers.
/// </summary>
public class PixelMinigameLimits : MonoBehaviour
{
    [Tooltip("Turn the limit on or off. Off = minigames spawn completely independently, as before.")]
    [SerializeField] private bool limitAtOnce = true;

    [Tooltip("Off (default) = only ONE minigame can have something on screen at a time: nothing spawns while another is busy. Tick this to use the Min / Max At Once numbers below instead (needs 'Limit At Once' on).")]
    [SerializeField] private bool allowOverlap = false;

    [Min(0f)]
    [Tooltip("No minigame spawns for this many seconds after the player presses Play (or after the game starts, without a title screen).")]
    [SerializeField] private float quietSecondsAfterPlay = 30f;

    [Min(1)]
    [Tooltip("At least this many minigames can be running together.")]
    [SerializeField] private int minAtOnce = 2;

    [Min(1)]
    [Tooltip("At most this many minigames can be running together. A new spawn rolls a number between Min and Max to decide how many are allowed right now.")]
    [SerializeField] private int maxAtOnce = 3;

    [Min(0.5f)]
    [Tooltip("When a minigame is held back because too many are running, it tries again after this many seconds.")]
    [SerializeField] private float retrySeconds = 4f;

    [Header("Breathers between minigames")]
    [Tooltip("Off = no pause between minigames (the next waiting one starts the moment another ends).")]
    [SerializeField] private bool disableBreathers = false;

    [Min(0f)]
    [Tooltip("After a minigame (or a visit) ends, nothing else starts for at least this many seconds. A random time between this and 'Breather Max Seconds' is used each time. 0 = the coded default (40).")]
    [SerializeField] private float breatherMinSeconds = 0f;

    [Min(0f)]
    [Tooltip("Longest breather after a minigame ends (seconds). 0 = the coded default (90).")]
    [SerializeField] private float breatherMaxSeconds = 0f;

    [Min(0f)]
    [Tooltip("A takeover round (Snake, Sorting Race, Breakout...) takes over the whole floor, so its breather is this many times longer. 0 = the coded default (1.5).")]
    [SerializeField] private float takeoverBreatherFactor = 0f;

    private static PixelMinigameLimits instance;
    private bool wasBusy, wasTakeover;
    private float breatherUntil;   // Time.time before which nothing may start

    private void Update()
    {
        // Watch for the moment the last busy minigame finishes: then a breather starts, so waiting minigames don't follow one another straight away.
        bool busy = false, takeover = false;
        foreach (PixelMinigame m in PixelMinigame.All)
            if (m != null && m.Running && m.Busy) { busy = true; if (m.Takeover) takeover = true; }
        if (wasBusy && !busy && !disableBreathers)
        {
            float lo = breatherMinSeconds > 0f ? breatherMinSeconds : 40f;
            float hi = Mathf.Max(lo, breatherMaxSeconds > 0f ? breatherMaxSeconds : 90f);
            float length = Random.Range(lo, hi) * (wasTakeover ? (takeoverBreatherFactor > 0f ? takeoverBreatherFactor : 1.5f) : 1f);
            breatherUntil = Time.time + length;
        }
        if (busy) wasTakeover = wasTakeover || takeover; else if (!wasBusy) wasTakeover = false;
        wasBusy = busy;
    }

    private void Awake()
    {
        if (instance == null) instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    /// <summary>Seconds to wait before asking again after being held back.</summary>
    public static float RetrySeconds => (instance != null ? instance.retrySeconds : 4f) * Random.Range(0.7f, 1.6f);   // (a little random, so several waiting minigames do not all try on the same frame)

    /// <summary>May another minigame spawn now? 'self' is the one asking (it doesn't count itself).</summary>
    public static bool AllowAnother(PixelMinigame self, bool playerStarted = false)
    {
        if (instance == null)
        {
            PixelClicker host = PixelFind.First<PixelClicker>();
            if (host != null) host.gameObject.AddComponent<PixelMinigameLimits>();
            if (instance == null) return true;
        }
        if (Time.time - PixelTitleScreen.PlayTime < instance.quietSecondsAfterPlay) return false; // a calm start
        if (!playerStarted && Time.time < instance.breatherUntil) return false;   // a breather after the last minigame ended (a round the player starts by clicking, like a fish, ignores it)
        // While a takeover round (Snake, Sorting Race, Breakout) is running, nothing else spawns.
        foreach (PixelMinigame m in PixelMinigame.All)
            if (m != null && m != self && m.Takeover && m.Busy) return false;
        if (!instance.allowOverlap)
        {
            // One at a time: any other minigame (or visitor) with something on screen holds this one back.
            foreach (PixelMinigame m in PixelMinigame.All)
                if (m != null && m != self && m.Running && m.Busy) return false;
            return true;
        }
        if (!instance.limitAtOnce) return true;

        int busy = 0;
        foreach (PixelMinigame m in PixelMinigame.All)
            if (m != null && m != self && m.Running && m.Busy) busy++;

        int low = Mathf.Min(instance.minAtOnce, instance.maxAtOnce), high = Mathf.Max(instance.minAtOnce, instance.maxAtOnce);
        return busy < Random.Range(low, high + 1);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; }
}
