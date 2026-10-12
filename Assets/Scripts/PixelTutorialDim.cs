using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared by the guided tutorials (shop, skills): fades the screen a little darker, pauses the game and blocks clicks on everything
/// below the tip boxes while a tutorial runs. <see cref="Begin"/> calls back once the screen is dark; <see cref="End"/> fades back and
/// resumes. <see cref="Active"/> is true in between, so a tutorial's own buttons can ignore clicks.
/// </summary>
public class PixelTutorialDim : MonoBehaviour
{
    private static PixelTutorialDim instance;

    /// <summary>True from <see cref="Begin"/> until <see cref="End"/>: the buttons of the windows being explained ignore clicks.</summary>
    public static bool Active { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; Active = false; }

    private GameObject root;
    private Image image;
    private float level;
    private bool darken;
    private System.Action onDark;
    private float amount = 0.4f, seconds = 0.7f;
    private bool pausedByUs;
    private float savedScale = 1f;

    /// <summary>Fades darker (amount 0 = 0.4, seconds 0 = 0.7), pauses the game and calls 'whenDark' when the screen is dark.</summary>
    public static void Begin(System.Action whenDark, float amount = 0f, float seconds = 0f)
    {
        if (instance == null)
        {
            GameObject go = new GameObject("Tutorial Dim");
            instance = go.AddComponent<PixelTutorialDim>();
        }
        instance.StartDim(whenDark, amount, seconds);
    }

    /// <summary>Fades back, resumes the game and unlocks the buttons.</summary>
    public static void End()
    {
        Active = false;
        if (instance != null) instance.StopDim();
    }

    private void StartDim(System.Action whenDark, float a, float s)
    {
        amount = a > 0f ? a : 0.4f;
        seconds = s > 0f ? s : 0.7f;
        Active = true;
        onDark = whenDark;
        if (root == null)
        {
            root = PixelUIKit.CreateCanvas("Tutorial Dim Canvas", 600, new Vector2(1920f, 1080f), true);
            root.transform.SetParent(transform, false);
            GameObject go = new GameObject("Dim", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(root.transform, false);
            image = go.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = true;   // clicks on the windows below the tip boxes do nothing
            PixelUIKit.Stretch(go.GetComponent<RectTransform>());
        }
        if (Time.timeScale > 0f) { savedScale = Time.timeScale; Time.timeScale = 0f; pausedByUs = true; }
        darken = true;
    }

    private void StopDim()
    {
        darken = false;
        onDark = null;
        if (pausedByUs) { if (Time.timeScale <= 0f) Time.timeScale = savedScale; pausedByUs = false; }
    }

    private void Update()
    {
        float goal = darken ? 1f : 0f;
        if (!Mathf.Approximately(level, goal)) level = Mathf.MoveTowards(level, goal, Time.unscaledDeltaTime / seconds);
        if (image != null) image.color = new Color(0f, 0f, 0f, level * amount);
        if (darken && level >= 1f && onDark != null)
        {
            System.Action go = onDark;
            onDark = null;
            go();
        }
        if (!darken && level <= 0f && root != null) { Destroy(root); root = null; image = null; }
    }

    private void OnDestroy()
    {
        if (pausedByUs && Time.timeScale <= 0f) Time.timeScale = savedScale;
        if (instance == this) { instance = null; Active = false; }
    }
}
