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

    private enum Mode { Idle, Grow, Shrink }
    private Mode mode = Mode.Idle;
    private float t;
    private GameObject hideTarget;

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

    private void StartGrow()
    {
        mode = Mode.Grow;
        t = 0f;
        hideTarget = null;
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
            transform.localScale = Vector3.one * Mathf.Max(smallScale, Mathf.Lerp(smallScale, 1f, back));
            if (k >= 1f) { mode = Mode.Idle; transform.localScale = Vector3.one; }
        }
        else
        {
            float k = Mathf.Clamp01(t / Mathf.Max(0.01f, shrinkSeconds));
            transform.localScale = Vector3.one * Mathf.Lerp(1f, smallScale, k * k);
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
