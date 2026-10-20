// ---- System notes ----
// PixelTimeControl.cs
//   Dev tool behind the Dev Hub's 'Time control' widget (PixelDevTools): Pause, Play (normal 1x), Slow motion, Fast forward and Rewind.
//   - Slow / Fast set Time.timeScale to Speed (Time.fixedDeltaTime follows below 1x so slow physics stays smooth, like Time Slow does). Pressing
//     the same button again steps through its presets (slowPresets / fastPresets / rewindPresets).
//   - While Slow, Fast or Rewind is on, the plain mouse wheel (not over UI, not with Ctrl, not in God mode) raises / lowers Speed by speedStep
//     (0.1x), between minSpeed and maxSpeed (0.1x - 5x). The mode follows the speed: Slow below 1x, Fast above it.
//   - REWIND: the husks' positions are recorded every recordInterval seconds of game time (up to recordSeconds, a ring of snapshots). Rewind
//     freezes the game (timeScale 0) and walks the husks back through the snapshots at Speed x real time; Pause / Play / Slow / Fast stop it there,
//     drop the history after that point and give the husks their recorded velocity back. ONLY husks rewind: currency, timers, minigames and
//     devices stay as they are. A husk reused by the pool (PixelHuskPool.HuskReleasing) is skipped for the snapshots taken before it was reused.
//   - Other systems still set timeScale (menus, Time Stop, notices): Slow / Fast are put back whenever time runs at another speed outside a menu,
//     Pause holds 0 outside menus. The title screen turns the tool off. A small label at the top of the screen shows the mode while it is on.
//   Only works in the editor / development builds (allowInFullBuild), like the other Dev Hub tools.
// ---------------------------------------------------
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PixelTimeControl : MonoBehaviour
{
    public enum Mode { Normal, Paused, Slow, Fast, Rewind }

    [Header("Speeds")]
    [Tooltip("Lowest speed the mouse wheel can set (x real time).")]
    [SerializeField] private float minSpeed = 0.1f;
    [Tooltip("Highest speed the mouse wheel can set (x real time).")]
    [SerializeField] private float maxSpeed = 5f;
    [Tooltip("How much one notch of the mouse wheel changes the speed.")]
    [SerializeField] private float speedStep = 0.1f;
    [Tooltip("Speeds the Slow button steps through when pressed again.")]
    [SerializeField] private float[] slowPresets = { 0.5f, 0.25f, 0.1f };
    [Tooltip("Speeds the Fast button steps through when pressed again.")]
    [SerializeField] private float[] fastPresets = { 2f, 3f, 5f };
    [Tooltip("Speeds the Rewind button steps through when pressed again.")]
    [SerializeField] private float[] rewindPresets = { 1f, 2f, 4f };

    [Header("Rewind")]
    [Tooltip("Seconds of game time between two recorded husk snapshots.")]
    [SerializeField] private float recordInterval = 0.1f;
    [Tooltip("How many seconds of game time can be rewound.")]
    [SerializeField] private float recordSeconds = 30f;

    [Header("Screen label")]
    [Tooltip("Show a small label at the top of the screen while time is not running normally.")]
    [SerializeField] private bool showLabel = true;
    [Tooltip("Font size of the screen label.")]
    [SerializeField] private float labelFontSize = 30f;
    [Tooltip("Colour of the screen label.")]
    [SerializeField] private Color labelColor = new Color(1f, 0.9f, 0.45f, 1f);
    [Tooltip("Gap between the label and the top of the screen (reference pixels).")]
    [SerializeField] private float labelTopMargin = 70f;

    [Header("Builds")]
    [Tooltip("Also work in a full (non-development) build.")]
    [SerializeField] private bool allowInFullBuild = false;

    /// <summary>The live tool (null in full builds).</summary>
    public static PixelTimeControl Instance { get; private set; }
    /// <summary>Raised when the mode or speed changes (the Dev Hub widget redraws its status line).</summary>
    public static event Action Changed;

    public Mode Current { get; private set; } = Mode.Normal;
    public float Speed { get; private set; } = 1f;
    /// <summary>Seconds of husk history that can be rewound right now.</summary>
    public float RecordedSeconds => frames.Count * Mathf.Max(0.01f, recordInterval);

    private struct Pose { public Rigidbody body; public Vector3 pos; public Quaternion rot; public Vector3 vel; public Vector3 angVel; }
    private class Frame { public int serial; public readonly List<Pose> poses = new List<Pose>(); }

    private PixelClicker clicker;
    private readonly List<Frame> frames = new List<Frame>();        // oldest first
    private readonly Stack<Frame> spare = new Stack<Frame>();
    private readonly Dictionary<Rigidbody, int> releasedAt = new Dictionary<Rigidbody, int>();   // body -> serial of the newest snapshot when it was released
    private int nextSerial;
    private float recordTimer;
    private float rewindPos;       // position in 'frames' while rewinding (float, between two snapshots)
    private float baseFixedDelta = 0.02f;
    private TMP_Text label;

    private void Awake()
    {
        if (!allowInFullBuild && !Application.isEditor && !Debug.isDebugBuild) { enabled = false; return; }
        Instance = this;
        baseFixedDelta = Time.fixedDeltaTime > 0f ? Time.fixedDeltaTime : 0.02f;
        clicker = PixelFind.Cached<PixelClicker>();
    }

    private void OnEnable() => PixelHuskPool.HuskReleasing += OnHuskReleasing;
    private void OnDisable() => PixelHuskPool.HuskReleasing -= OnHuskReleasing;

    private void OnDestroy()
    {
        if (Instance != this) return;
        if (Current != Mode.Normal) { Time.timeScale = 1f; Time.fixedDeltaTime = baseFixedDelta; }
        Instance = null;
    }

    private void OnHuskReleasing(Rigidbody body) { if (body != null && frames.Count > 0) releasedAt[body] = frames[frames.Count - 1].serial; }

    // ---------------- Buttons ----------------

    public void Pause() { EndRewind(); SetMode(Mode.Paused, Speed); }
    public void Play() { EndRewind(); SetMode(Mode.Normal, 1f); }
    public void Slow() { EndRewind(); SetMode(Mode.Slow, NextPreset(slowPresets, Mode.Slow)); }
    public void Fast() { EndRewind(); SetMode(Mode.Fast, NextPreset(fastPresets, Mode.Fast)); }

    public void Rewind()
    {
        float s = NextPreset(rewindPresets, Mode.Rewind);
        if (Current != Mode.Rewind)
        {
            if (frames.Count == 0) { PixelNotice.Show("Nothing to rewind yet - husk positions are recorded while time runs.", small: true); return; }
            rewindPos = frames.Count - 1;
        }
        SetMode(Mode.Rewind, s);
    }

    private float NextPreset(float[] presets, Mode mode)
    {
        if (presets == null || presets.Length == 0) return 1f;
        if (Current != mode) return presets[0];
        int i = Array.FindIndex(presets, p => Mathf.Approximately(p, Speed));
        return presets[(i + 1) % presets.Length];   // (not a preset any more, e.g. after scrolling: back to the first one)
    }

    private void SetMode(Mode mode, float speed)
    {
        Current = mode;
        Speed = Mathf.Clamp(speed, minSpeed, maxSpeed);
        Apply();
        RefreshLabel();
        Changed?.Invoke();
    }

    /// <summary>Puts timeScale / fixedDeltaTime where the mode wants them.</summary>
    private void Apply()
    {
        switch (Current)
        {
            case Mode.Normal: Time.timeScale = 1f; Time.fixedDeltaTime = baseFixedDelta; break;
            case Mode.Paused:
            case Mode.Rewind: Time.timeScale = 0f; break;
            default:
                Time.timeScale = Speed;
                Time.fixedDeltaTime = baseFixedDelta * Mathf.Min(1f, Speed);   // (slow physics keeps its smoothness; fast keeps the normal step)
                break;
        }
    }

    // ---------------- Every frame ----------------

    private void Update()
    {
        if (PixelTitleScreen.Showing) { if (Current != Mode.Normal) Play(); return; }
        if (Current == Mode.Normal) return;
        if (!PixelCalm.InMenu)
        {
            // Something else changed the time (Time Stop ending, a notice closing): put ours back.
            if (Current == Mode.Paused || Current == Mode.Rewind) { if (Time.timeScale != 0f) Time.timeScale = 0f; }
            else if (Time.timeScale > 0f && !Mathf.Approximately(Time.timeScale, Speed)) Apply();
            HandleWheel();
        }
        if (Current == Mode.Rewind && !PixelCalm.InMenu) StepRewind();
    }

    private void HandleWheel()
    {
        if (Current == Mode.Paused || PixelInput.PointerOverUI() || PixelInput.ControlHeld() || PixelClicker.GodMode) return;
        float scroll = PixelInput.ScrollY();
        if (Mathf.Abs(scroll) < 0.01f) return;
        float s = Mathf.Round((Speed + Mathf.Sign(scroll) * speedStep) * 10f) / 10f;
        s = Mathf.Clamp(s, minSpeed, maxSpeed);
        Mode m = Current == Mode.Rewind ? Mode.Rewind : s < 1f ? Mode.Slow : s > 1f ? Mode.Fast : Current;
        SetMode(m, s);
    }

    private void FixedUpdate()
    {
        // Record while time runs forward (FixedUpdate does not run at timeScale 0, so pausing / rewinding records nothing).
        if (Current == Mode.Rewind || clicker == null) return;
        recordTimer += Time.fixedDeltaTime;
        if (recordTimer < Mathf.Max(0.01f, recordInterval)) return;
        recordTimer = 0f;
        Record();
    }

    private void Record()
    {
        int max = Mathf.Max(1, Mathf.RoundToInt(recordSeconds / Mathf.Max(0.01f, recordInterval)));
        while (frames.Count >= max) { spare.Push(frames[0]); frames.RemoveAt(0); }
        Frame f = spare.Count > 0 ? spare.Pop() : new Frame();
        f.serial = nextSerial++;
        f.poses.Clear();
        var list = clicker.OldPixels;
        for (int i = 0; i < list.Count; i++)
        {
            Rigidbody b = list[i];
            if (b == null) continue;
            f.poses.Add(new Pose { body = b, pos = b.position, rot = b.rotation, vel = PixelBody.Velocity(b), angVel = b.angularVelocity });
        }
        frames.Add(f);
        if (releasedAt.Count > 0 && frames.Count > 0) PruneReleased(frames[0].serial);
    }

    /// <summary>Forgets released husks whose release is older than every kept snapshot.</summary>
    private void PruneReleased(int oldestSerial)
    {
        if (releasedAt.Count < 64) return;
        List<Rigidbody> gone = null;
        foreach (var kv in releasedAt) if (kv.Value < oldestSerial || kv.Key == null) (gone ??= new List<Rigidbody>()).Add(kv.Key);
        if (gone != null) foreach (Rigidbody b in gone) releasedAt.Remove(b);
    }

    private void StepRewind()
    {
        float perSecond = Speed / Mathf.Max(0.01f, recordInterval);   // snapshots per real second
        rewindPos = Mathf.Max(0f, rewindPos - perSecond * Time.unscaledDeltaTime);
        ShowFrame(Mathf.RoundToInt(rewindPos));
        if (rewindPos <= 0f) Pause();   // (the start of the history: stop there)
    }

    private void ShowFrame(int index)
    {
        if (index < 0 || index >= frames.Count) return;
        Frame f = frames[index];
        foreach (Pose p in f.poses)
        {
            if (p.body == null) continue;
            if (releasedAt.TryGetValue(p.body, out int at) && f.serial <= at) continue;   // (the pool reused it: it is another husk now)
            p.body.position = p.pos;
            p.body.rotation = p.rot;
            p.body.transform.SetPositionAndRotation(p.pos, p.rot);
        }
    }

    /// <summary>Leaving Rewind: the husks keep the recorded spot and speed, and the history after that point is dropped.</summary>
    private void EndRewind()
    {
        if (Current != Mode.Rewind) return;
        int index = Mathf.Clamp(Mathf.RoundToInt(rewindPos), 0, frames.Count - 1);
        if (index < 0) return;
        Frame f = frames[index];
        foreach (Pose p in f.poses)
        {
            if (p.body == null || p.body.isKinematic) continue;
            if (releasedAt.TryGetValue(p.body, out int at) && f.serial <= at) continue;
            PixelBody.SetVelocity(p.body, p.vel);
            p.body.angularVelocity = p.angVel;
            p.body.WakeUp();
        }
        while (frames.Count > index + 1) { spare.Push(frames[frames.Count - 1]); frames.RemoveAt(frames.Count - 1); }
        recordTimer = 0f;
    }

    // ---------------- Screen label ----------------

    public string StatusText()
    {
        switch (Current)
        {
            case Mode.Paused: return "Paused";
            case Mode.Slow: return "Slow motion " + Speed.ToString("0.0") + "x";
            case Mode.Fast: return "Fast forward " + Speed.ToString("0.0") + "x";
            case Mode.Rewind: return "Rewind " + Speed.ToString("0.0") + "x  (" + (rewindPos * recordInterval).ToString("0.0") + " s left)";
            default: return "Normal 1.0x";
        }
    }

    private void RefreshLabel()
    {
        if (!showLabel) return;
        if (label == null)
        {
            if (Current == Mode.Normal) return;
            GameObject canvas = PixelUIKit.CreateCanvas("Time Control Label", 900, new Vector2(1920f, 1080f), false);
            canvas.transform.SetParent(transform, false);
            label = PixelUIKit.CreateText(clicker != null ? clicker.UIFont : null, canvas.transform, "Label", "", labelFontSize,
                                          TextAlignmentOptions.Center, FontStyles.Bold, labelColor);
            RectTransform r = label.rectTransform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 1f);
            r.sizeDelta = new Vector2(900f, labelFontSize * 1.4f);
            r.anchoredPosition = new Vector2(0f, -labelTopMargin);
        }
        label.gameObject.SetActive(Current != Mode.Normal);
        label.text = StatusText() + (Current == Mode.Paused || Current == Mode.Normal ? "" : "   (mouse wheel: speed)");
    }

    private void LateUpdate() { if (Current == Mode.Rewind && label != null) label.text = StatusText() + "   (mouse wheel: speed)"; }
}
