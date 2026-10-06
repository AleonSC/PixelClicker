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

    /// <summary>Seconds left.</summary>
    public float Remaining => remaining;

    /// <summary>True once time ran out and the device is shrinking away (it should stop working).</summary>
    protected bool IsDying => dying;

    /// <summary>Is the device already shrinking away?</summary>
    public bool IsRemoving => dying;

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
