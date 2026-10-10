using UnityEngine;
using static PixelInput;

/// <summary>
/// An old Seed pixel: it slowly grows (bigger, and worth more: its <see cref="OldPixelInfo.amount"/> rises up to
/// <c>maxMultiplier</c> times) and doesn't despawn while it grows. Once ripe it pulses gently and can be harvested with a click
/// (or by a vacuum, the hose or anything else that collects old pixels, which all pay the current amount).
/// Pixels spawned from a stored value (loaded saves, the Bank) are treated as already grown so the value doesn't compound.
/// </summary>
public class OldPixelSeed : MonoBehaviour
{
    private PixelClicker clicker;
    private float growSeconds, maxScale, maxMultiplier;
    private bool alreadyGrown;

    private OldPixelInfo info;
    private OldPixelDespawn despawn;
    private Rigidbody body;
    private Collider hit;
    private Vector3 baseScale;
    private double baseAmount;
    private float age;
    private bool started;
    private static int lastHarvestFrame = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { lastHarvestFrame = -1; }

    public void Setup(PixelClicker owner, float seconds, float scale, float multiplier, bool grown)
    {
        clicker = owner;
        growSeconds = Mathf.Max(0.1f, seconds);
        maxScale = Mathf.Max(1f, scale);
        maxMultiplier = Mathf.Max(1f, multiplier);
        alreadyGrown = grown;
    }

    private void Update()
    {
        if (clicker == null) return;
        if (GetComponent<OldPixelPopIn>() != null) return; // wait for the pop-in to finish

        if (!started)
        {
            info = GetComponent<OldPixelInfo>();
            despawn = GetComponent<OldPixelDespawn>();
            body = GetComponent<Rigidbody>();
            hit = GetComponent<Collider>();
            if (info == null) return; // not set up yet
            started = true;
            baseScale = transform.localScale;
            baseAmount = info.amount;
            if (alreadyGrown)
            {
                age = growSeconds;
                transform.localScale = baseScale * maxScale;
            }
        }
        if (despawn != null && despawn.IsDespawning) { enabled = false; return; }

        bool ripe = age >= growSeconds;
        if (!ripe)
        {
            if (!(despawn != null && despawn.Held)) age += Time.deltaTime;
            float k = Mathf.Clamp01(age / growSeconds);
            float ease = k * k * (3f - 2f * k);
            transform.localScale = baseScale * Mathf.Lerp(1f, maxScale, ease);
            if (!alreadyGrown) info.amount = baseAmount * Mathf.Lerp(1d, maxMultiplier, ease);
            if (despawn != null) despawn.KeepAlive(); // it lives until it is fully grown
            return;
        }

        // Ripe: a gentle pulse, and a click harvests it.
        float pulse = 1f + 0.03f * Mathf.Sin(Time.time * 6f + GetInstanceID() * 0.37f);
        transform.localScale = baseScale * (maxScale * pulse);
        if (!alreadyGrown) info.amount = baseAmount * maxMultiplier;
        TryHarvestByClick();
    }

    private void TryHarvestByClick()
    {
        if (!LeftPressed() || Time.timeScale <= 0f || PointerOverUI() || PixelClicker.GodMode || PixelClicker.ExternalClickBlock) return;
        if (PixelMinigame.TakeoverActive || hit == null || body == null || lastHarvestFrame == Time.frameCount) return;
        Camera cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
        if (cam == null) return;
        if (!hit.Raycast(cam.ScreenPointToRay(PointerPosition()), out RaycastHit _, 1000f)) return;

        lastHarvestFrame = Time.frameCount;
        if (!clicker.ReleaseOldPixel(body, true)) return; // pays the grown amount
        PixelStats.Count("seed.harvested");
        PixelAudio.Play("click");
        if (despawn != null) despawn.Begin();
    }
}
