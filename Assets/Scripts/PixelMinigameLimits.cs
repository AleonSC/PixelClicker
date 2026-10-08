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
