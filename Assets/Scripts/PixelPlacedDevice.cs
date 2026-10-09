using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Shared behaviour of the devices you place in the world (vacuum, fan): a countdown with a timer text floating above
/// the device that always faces the camera, and a shrink-away when time runs out. Subclasses add what the device does
/// (FixedUpdate) and may react to every tick (OnTick).
/// </summary>
public abstract class PixelPlacedDevice : MonoBehaviour
{
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() // keeps static state clean when Enter Play Mode skips the domain reload
    {
        all.Clear();
    }

    private static readonly List<PixelPlacedDevice> all = new List<PixelPlacedDevice>();

    /// <summary>Every placed device that exists right now.</summary>
    public static IReadOnlyList<PixelPlacedDevice> All => all;

    protected PixelClicker clicker;
    protected Camera cam;

    private TextMeshPro timerText;
    private string timerFormat;
    private float remaining;
    private float shrinkSeconds;
    private bool dying;

    /// <summary>Which entry of the consumables' device list this is (set when it is placed; used by the save system).</summary>
    public int DeviceIndex { get; set; } = -1;

    private bool usesMode;
    private int usesLeft;

    /// <summary>Seconds left (in uses mode: the number of uses left, so the save system stores either).</summary>
    public float Remaining => usesMode ? usesLeft : remaining;

    /// <summary>True for devices that last a number of uses (clicks, lightning strikes) instead of a time.</summary>
    public bool UsesMode => usesMode;

    /// <summary>Uses left (uses mode only).</summary>
    public int UsesLeft => usesLeft;

    /// <summary>Switches the device from a countdown to a number of uses. Call after InitCommon.</summary>
    protected void InitUses(int uses)
    {
        usesMode = true;
        usesLeft = Mathf.Max(1, uses);
    }

    /// <summary>What the floating text shows in uses mode.</summary>
    protected virtual string UsesLabel() => usesLeft.ToString();

    /// <summary>Uses up some uses; the device dissolves at 0. No effect once it is shrinking away.</summary>
    protected void SpendUse(int count = 1)
    {
        if (!usesMode || dying) return;
        usesLeft -= count;
        if (usesLeft <= 0)
        {
            usesLeft = 0;
            StartCoroutine(ShrinkAway());
        }
    }

    /// <summary>True once time ran out and the device is shrinking away (it should stop working).</summary>
    protected bool IsDying => dying;

    /// <summary>Is the device already shrinking away?</summary>
    public bool IsRemoving => dying;

    /// <summary>Raised once when the device's time is up and it starts shrinking away (also when it is removed by hand).</summary>
    public event System.Action<PixelPlacedDevice> Ended;

    /// <summary>Adds (or, negative, takes) seconds to the countdown. No effect once the device is shrinking away.</summary>
    public void AddSeconds(float seconds)
    {
        if (!dying && !usesMode) remaining += seconds;
    }

    /// <summary>Removes the device now (it shrinks away like when its time runs out). No refund.</summary>
    public void RemoveNow()
    {
        if (!dying) StartCoroutine(ShrinkAway());
    }

    private void OnEnable() => all.Add(this);

    private void OnDisable() => all.Remove(this);

    /// <summary>Sets up the countdown. Called by the subclass's own Init.</summary>
    protected void InitCommon(PixelClicker owner, Camera camera, TextMeshPro timer, string format, float duration, float shrinkTime)
    {
        clicker = owner;
        cam = camera;
        timerText = timer;
        timerFormat = format;
        remaining = duration;
        shrinkSeconds = Mathf.Max(0.01f, shrinkTime);
    }

    /// <summary>Called every frame while the device is still working.</summary>
    protected virtual void OnTick() { }

    private void Update()
    {
        if (dying) return;

        if (usesMode)
        {
            OnTick();
            if (dying) return;
            if (timerText != null) timerText.text = UsesLabel();
            return;
        }

        remaining -= Time.deltaTime;
        if (remaining <= 0f)
        {
            StartCoroutine(ShrinkAway());
            return;
        }

        OnTick();
        if (timerText != null) timerText.text = string.Format(timerFormat, Mathf.CeilToInt(remaining));
    }

    private void LateUpdate()
    {
        // The timer always faces the camera.
        if (timerText == null) return;
        if (cam == null) cam = Camera.main;
        if (cam != null) timerText.transform.rotation = cam.transform.rotation;
    }

    private IEnumerator ShrinkAway()
    {
        dying = true;
        Ended?.Invoke(this);
        if (timerText != null) timerText.gameObject.SetActive(false);

        Vector3 start = transform.localScale;
        float t = 0f;
        while (t < shrinkSeconds)
        {
            t += Time.deltaTime;
            transform.localScale = start * (1f - Mathf.Clamp01(t / shrinkSeconds));
            yield return null;
        }
        Destroy(gameObject);
    }
}
