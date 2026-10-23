using UnityEngine;

/// <summary>
/// Flat view: switches the main camera from perspective to orthographic, so there is no depth shrinking (things far away are as big as
/// things close up). The camera keeps its position and angle, and the size is chosen so the cube looks as big as it did before.
///
/// Player switch: Settings > Display > Flat view (<see cref="Enabled"/>, global PlayerPrefs, synced per save). The change is blended in over
/// <see cref="blendSeconds"/> by mixing the two projection matrices. While the title screen shows, the camera intro moves the camera or the
/// camera swings back to the main menu, the view is perspective (the intro lerps the field of view), and the blend starts when that ends.
///
/// Added automatically by PixelClicker. Add it to any GameObject yourself to change its settings in the Inspector.
/// </summary>
[DefaultExecutionOrder(200)]   // after the zoom / orbit cameras, which also write the camera in LateUpdate
public class PixelOrthoView : MonoBehaviour
{
    private const string PrefEnabled = "PixelClicker.Setting.FlatView";

    [Header("References")]
    [Tooltip("The PixelClicker whose camera is switched. Found automatically if left empty.")]
    [SerializeField] private PixelClicker clicker;

    [Header("Flat view")]
    [Min(0f)]
    [Tooltip("Seconds the camera takes to change between the normal and the flat view. 0 = 0.6.")]
    [SerializeField] private float blendSeconds = 0f;

    [Min(0f)]
    [Tooltip("Size of the flat view compared with the normal one at the cube (1 = the cube looks the same size, 0.8 = zoomed in a little). 0 = 1.")]
    [SerializeField] private float sizeFactor = 0f;

    [Tooltip("Never use the flat view, whatever the player's setting says.")]
    [SerializeField] private bool disableFlatView = false;

    private static int enabledCache = -1;
    private static PixelOrthoView instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { enabledCache = -1; instance = null; Blend = 0f; }

    /// <summary>Player setting (Settings > Display > Flat view), off by default.</summary>
    public static bool Enabled
    {
        get
        {
            if (enabledCache < 0) enabledCache = PixelPrefs.GetInt(PrefEnabled, 0);
            return enabledCache != 0;
        }
        set
        {
            enabledCache = value ? 1 : 0;
            PixelPrefs.SetInt(PrefEnabled, enabledCache);
            PixelPrefs.Save();
        }
    }

    /// <summary>Called after a save file changed the settings.</summary>
    public static void ReloadFromPrefs() { enabledCache = -1; }

    /// <summary>Back to the perspective view at once (first person copies the camera, so it must not be flat when it starts).</summary>
    public static void ForcePerspectiveNow()
    {
        if (instance == null) return;
        Blend = 0f;
        instance.lastBlend = 0f;
        if (instance.cam != null) instance.Restore();
    }

    /// <summary>How flat the view is right now: 0 = perspective, 1 = orthographic.</summary>
    public static float Blend { get; private set; }

    private Camera cam;
    private bool wasMatrixSet, madeOrtho;
    private float lastBlend = -1f;

    private void Awake()
    {
        instance = this;
        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        Blend = 0f;
        if (cam != null) Restore();
    }

    private void LateUpdate()
    {
        if (clicker == null) return;
        Camera now = clicker.ViewCamera;
        if (now == null) return;
        if (cam != now) { if (cam != null) Restore(); cam = now; lastBlend = -1f; }

        // The intro and the title screen's close-up work on the field of view, so they get the normal perspective camera.
        bool busy = PixelGameMode.TitleOrIntro;
        float target = Enabled && !disableFlatView && !busy && !PixelTitleScreen.Reversing && !PixelFirstPerson.Active && !PixelFreeCam.Active ? 1f : 0f;   // (first person and the free cam are always the 3D view)
        if (busy) Blend = 0f;
        else
        {
            float seconds = blendSeconds > 0f ? blendSeconds : 0.6f;
            Blend = Mathf.MoveTowards(Blend, target, Time.unscaledDeltaTime / seconds);
        }

        if (Blend <= 0f && lastBlend <= 0f) return;   // perspective and nothing to undo
        lastBlend = Blend;
        Apply();   // every frame while flat: the size follows the cube's distance, so the zoom key and the orbit camera still work
    }

    private void Apply()
    {
        if (Blend <= 0f) { Restore(); return; }

        // The orthographic height that matches the perspective view at the cube's distance, so the cube keeps its size.
        float depth = 10f;
        if (clicker.PixelTransform != null)
            depth = Mathf.Max(cam.nearClipPlane + 0.5f, Vector3.Dot(clicker.CubeBasePosition - cam.transform.position, cam.transform.forward));
        float half = depth * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * (sizeFactor > 0f ? sizeFactor : 1f);

        if (Blend >= 1f)
        {
            if (wasMatrixSet) cam.ResetProjectionMatrix();
            wasMatrixSet = false;
            if (!cam.orthographic) madeOrtho = true;
            cam.orthographic = true;
            cam.orthographicSize = half;
            return;
        }

        // In between: mix the perspective and orthographic matrices (the camera stays "perspective" for the engine while it blends).
        if (madeOrtho) { cam.orthographic = false; madeOrtho = false; }
        cam.ResetProjectionMatrix();
        Matrix4x4 persp = cam.projectionMatrix;
        Matrix4x4 ortho = Matrix4x4.Ortho(-half * cam.aspect, half * cam.aspect, -half, half, cam.nearClipPlane, cam.farClipPlane);
        float k = Blend * Blend * (3f - 2f * Blend);
        Matrix4x4 mix = new Matrix4x4();
        for (int i = 0; i < 16; i++) mix[i] = Mathf.Lerp(persp[i], ortho[i], k);
        cam.projectionMatrix = mix;
        wasMatrixSet = true;
    }

    private void Restore()
    {
        if (cam == null) return;
        if (wasMatrixSet) cam.ResetProjectionMatrix();
        wasMatrixSet = false;
        if (madeOrtho) cam.orthographic = false;   // (a camera that is orthographic in the scene is left alone)
        madeOrtho = false;
    }
}
