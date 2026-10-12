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

    private static PixelMinigameLimits instance;

    private void Awake()
    {
        if (instance == null) instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    /// <summary>Seconds to wait before asking again after being held back.</summary>
    public static float RetrySeconds => instance != null ? instance.retrySeconds : 4f;

    /// <summary>May another minigame spawn now? 'self' is the one asking (it doesn't count itself).</summary>
    public static bool AllowAnother(PixelMinigame self)
    {
        if (instance == null)
        {
            PixelClicker host = PixelFind.First<PixelClicker>();
            if (host != null) host.gameObject.AddComponent<PixelMinigameLimits>();
            if (instance == null) return true;
        }
        if (Time.time - PixelTitleScreen.PlayTime < instance.quietSecondsAfterPlay) return false; // a calm start
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
