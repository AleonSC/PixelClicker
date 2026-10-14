// ---- System notes (moved here from CLAUDE.md) ----
// PixelPop.cs
//   Open / close animation for windows and tip boxes (component on the Inventory box, the Log panel, the Got-it box, the Shop panel,
//   the Crafting window, the Toggles window and every pause-menu screen: PixelPauseMenu.SetView pops the screen in, resuming shrinks
//   the open one): it grows from smallScale to full size on enable (growSeconds, small overshoot, unscaled time, scales around the
//   rect's pivot) and PixelPop.Hide(go[, deactivate]) shrinks it (shrinkSeconds) before deactivating; PixelPop.Show(go) also cancels a
//   shrink under way; PixelPop.IsOpen(go) = shown and not closing. Open / close such windows through Show / Hide / IsOpen, not
//   SetActive / activeSelf. Fit to screen: a fixed-size window (not stretched in x / y) that is bigger than the canvas between the
//   black bars is scaled down by PixelPop.ComputeFit (UI scale 130% makes the canvas only ~830 units tall, so the 880-tall shop would
//   be cut off).
// ---------------------------------------------------
using UnityEngine;

/// <summary>
/// Windows and tip boxes grow from small to full size when they open and shrink back down when they close, instead of
/// just appearing / vanishing. Put on a window's root; open and close it through <see cref="Show"/> / <see cref="Hide"/>
/// (a plain SetActive(true) also grows it, a plain SetActive(false) just hides it at once). Uses unscaled time, so it
/// also plays while the game is stopped. The window scales around its own pivot, so it grows out of its anchor corner.
/// </summary>
public class PixelPop : MonoBehaviour
{
    [Tooltip("Seconds a window takes to grow to full size.")]
    [SerializeField] private float growSeconds = 0.22f;
    [Tooltip("Seconds a window takes to shrink away.")]
    [SerializeField] private float shrinkSeconds = 0.16f;
    [Tooltip("Size a window starts to grow from / ends shrinking at (fraction of full size).")]
    [SerializeField] private float smallScale = 0.05f;

    private static float lastOpenSound = -10f;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOpenSound() { lastOpenSound = -10f; }

    private enum Mode { Idle, Grow, Shrink }
    private Mode mode = Mode.Idle;
    private float t;
    private GameObject hideTarget;
    private float fit = 1f;   // < 1 when a fixed-size window is bigger than the room between the black bars (high UI scale): it is shrunk to fit

    /// <summary>
    /// The scale that makes a fixed-size window fit on screen (between the black bars, inside the canvas width). Stretched windows
    /// (anchored to both top and bottom) are left alone: they already follow the screen.
    /// </summary>
    private float ComputeFit()
    {
        RectTransform rt = transform as RectTransform;
        if (rt == null || !Mathf.Approximately(rt.anchorMin.y, rt.anchorMax.y)) return 1f;   // stretched in height = already follows the screen
        bool fixedWidth = Mathf.Approximately(rt.anchorMin.x, rt.anchorMax.x);
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return 1f;
        RectTransform root = canvas.rootCanvas.transform as RectTransform;
        if (root == null) return 1f;
        float bar = PixelHud.Instance != null ? PixelHud.Instance.BarHeight + 4f : 0f;
        float availH = root.rect.height - bar * 2f, availW = root.rect.width - 20f;
        Vector2 size = rt.rect.size;
        if (size.x < 1f || size.y < 1f || availH < 50f) return 1f;
        float f = Mathf.Min(1f, availH / size.y, fixedWidth ? availW / size.x : 1f);

        // The window scales around its pivot, so a window sitting against one edge (the Log, the Inventory) must also keep its far side inside the bars:
        // how far the pivot is from the bottom / top bar limits how tall it can be.
        float pivotY = rt.pivot.y;
        float pivotFromBottom = root.InverseTransformPoint(rt.position).y - root.rect.yMin;   // the pivot's height above the bottom of the canvas
        float room = pivotFromBottom - bar;                                                   // down to the bottom bar
        float roomUp = root.rect.height - bar - pivotFromBottom;                              // up to the top bar
        if (pivotY > 0.001f && room > 0f) f = Mathf.Min(f, room / (pivotY * size.y));
        if (pivotY < 0.999f && roomUp > 0f) f = Mathf.Min(f, roomUp / ((1f - pivotY) * size.y));
        return Mathf.Clamp(f, 0.4f, 1f);
    }

    /// <summary>The scale this window was shrunk to so it fits the screen (1 = full size).</summary>
    public float Fit => fit;

    /// <summary>True while the window is shown and not on its way out.</summary>
    public static bool IsOpen(GameObject go)
    {
        if (go == null || !go.activeSelf) return false;
        PixelPop p = go.GetComponent<PixelPop>();
        return p == null || p.mode != Mode.Shrink;
    }

    /// <summary>Opens 'go' with the grow animation (also cancels a shrink that is under way).</summary>
    public static void Show(GameObject go)
    {
        if (go == null) return;
        PixelPop p = go.GetComponent<PixelPop>();
        if (p != null && go.activeSelf && p.mode == Mode.Shrink) { p.StartGrow(); return; }
        if (!go.activeSelf) go.SetActive(true);
    }

    /// <summary>Closes 'go' with the shrink animation, then deactivates it (or 'deactivate' instead, e.g. a canvas around it).</summary>
    public static void Hide(GameObject go, GameObject deactivate = null)
    {
        if (go == null) return;
        GameObject end = deactivate != null ? deactivate : go;
        PixelPop p = go.GetComponent<PixelPop>();
        if (p == null || !go.activeInHierarchy) { end.SetActive(false); return; }
        if (p.mode == Mode.Shrink) return;
        p.mode = Mode.Shrink;
        p.t = 0f;
        p.hideTarget = end;
    }

    private void OnEnable() { StartGrow(); }

    private void OnDisable()
    {
        mode = Mode.Idle;
        transform.localScale = Vector3.one;
    }

    private void LateUpdate()
    {
        if (mode != Mode.Idle) return;
        float f = ComputeFit();
        if (!Mathf.Approximately(f, fit) || !Mathf.Approximately(transform.localScale.x, f)) { fit = f; transform.localScale = Vector3.one * f; }
    }

    private void StartGrow()
    {
        if (Time.unscaledTime - lastOpenSound > 0.1f && Time.realtimeSinceStartup > 3f) { lastOpenSound = Time.unscaledTime; PixelAudio.Play("menu_open"); }   // a deep soft bloop whenever a menu / tip opens
        mode = Mode.Grow;
        t = 0f;
        hideTarget = null;
        fit = ComputeFit();
        transform.localScale = Vector3.one * smallScale;
    }

    private void Update()
    {
        if (mode == Mode.Idle) return;
        float dt = Time.unscaledDeltaTime;
        t += dt;
        if (mode == Mode.Grow)
        {
            float k = Mathf.Clamp01(t / Mathf.Max(0.01f, growSeconds));
            float c = 1.4f;                                   // gentle overshoot, then settles
            float back = 1f + (c + 1f) * Mathf.Pow(k - 1f, 3f) + c * Mathf.Pow(k - 1f, 2f);
            transform.localScale = Vector3.one * Mathf.Max(smallScale, Mathf.Lerp(smallScale, fit, back));
            if (k >= 1f) { mode = Mode.Idle; transform.localScale = Vector3.one * fit; }
        }
        else
        {
            float k = Mathf.Clamp01(t / Mathf.Max(0.01f, shrinkSeconds));
            transform.localScale = Vector3.one * Mathf.Lerp(fit, smallScale, k * k);
            if (k >= 1f)
            {
                mode = Mode.Idle;
                GameObject end = hideTarget != null ? hideTarget : gameObject;
                hideTarget = null;
                end.SetActive(false);                          // OnDisable puts the scale back to 1
            }
        }
    }
}
