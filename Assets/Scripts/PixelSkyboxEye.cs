using UnityEngine;

/// <summary>
/// The 'Watcher' sky (partial class PixelSkybox): the sky is pitch black, but every so often a glowing eye opens somewhere in the upper
/// sky, its pupil follows the mouse cursor (and the eye drifts a little towards it) for a while, then it closes and is gone for a good
/// while. The eye is three flat quads far away in front of the camera (behind the scene's own objects), built and drawn in code.
/// </summary>
public partial class PixelSkybox
{
    [Header("Watcher eye (the 'Watcher' sky)")]
    [Tooltip("Turns the eye off (the Watcher sky is then just black).")]
    [SerializeField] private bool disableEye = false;

    [Tooltip("Height of the open eye as a fraction of the screen height. 0 = the default (0.16).")]
    [SerializeField] private float eyeSize = 0f;

    [Tooltip("Shortest time the eye stays away, in seconds. 0 = the default (10).")]
    [SerializeField] private float eyeHiddenMin = 0f;

    [Tooltip("Longest time the eye stays away, in seconds. 0 = the default (28).")]
    [SerializeField] private float eyeHiddenMax = 0f;

    [Tooltip("Shortest time the eye watches the cursor, in seconds. 0 = the default (6).")]
    [SerializeField] private float eyeWatchMin = 0f;

    [Tooltip("Longest time the eye watches the cursor, in seconds. 0 = the default (14).")]
    [SerializeField] private float eyeWatchMax = 0f;

    private enum EyeState { Hidden, Opening, Watching, Closing }

    private GameObject eyeRoot;
    private Transform eyeWhite, eyeIris, eyeOutline;
    private Material[] eyeMats;
    private Texture2D[] eyeTextures;
    private EyeState eyeState = EyeState.Hidden;
    private bool eyeArmed;
    private float eyeTimer, eyeOpen, eyeBlink, eyeNextBlink;
    private Vector2 eyeView = new Vector2(0.5f, 0.7f);     // where the eye is on the screen (viewport)
    private Vector2 eyeLook;                                // pupil offset, -1..1 on each axis

    private float HiddenSeconds() => Random.Range(eyeHiddenMin > 0f ? eyeHiddenMin : 10f, Mathf.Max(eyeHiddenMin > 0f ? eyeHiddenMin : 10f, eyeHiddenMax > 0f ? eyeHiddenMax : 28f));
    private float WatchSeconds() => Random.Range(eyeWatchMin > 0f ? eyeWatchMin : 6f, Mathf.Max(eyeWatchMin > 0f ? eyeWatchMin : 6f, eyeWatchMax > 0f ? eyeWatchMax : 14f));

    private void LateUpdate()
    {
        Camera cam = Cam;
        bool want = !disableEye && cam != null && current >= 0 && current < styles.Count && styles[current].pattern == SkyPattern.EyeVoid;
        if (!want)
        {
            if (eyeRoot != null && eyeRoot.activeSelf) eyeRoot.SetActive(false);
            eyeState = EyeState.Hidden;
            eyeArmed = false;
            return;
        }
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        if (!eyeArmed) { eyeArmed = true; eyeTimer = Random.Range(2f, 6f); eyeState = EyeState.Hidden; }
        if (eyeRoot == null) BuildEye(styles[current]);

        Vector2 pointer = cam.ScreenToViewportPoint(PixelInput.PointerPosition());
        switch (eyeState)
        {
            case EyeState.Hidden:
                eyeTimer -= dt;
                if (eyeTimer <= 0f)
                {
                    // Somewhere in the upper sky, not right under the cursor.
                    for (int i = 0; i < 8; i++)
                    {
                        eyeView = new Vector2(Random.Range(0.15f, 0.85f), Random.Range(0.5f, 0.85f));
                        if ((eyeView - pointer).sqrMagnitude > 0.04f) break;
                    }
                    eyeOpen = 0f;
                    eyeLook = Vector2.zero;
                    eyeNextBlink = Random.Range(2f, 4f);
                    eyeState = EyeState.Opening;
                    eyeRoot.SetActive(true);
                }
                break;
            case EyeState.Opening:
                eyeOpen = Mathf.MoveTowards(eyeOpen, 1f, dt / 1.3f);
                if (eyeOpen >= 1f) { eyeState = EyeState.Watching; eyeTimer = WatchSeconds(); }
                break;
            case EyeState.Watching:
                eyeTimer -= dt;
                eyeNextBlink -= dt;
                if (eyeNextBlink <= 0f) { eyeBlink = 1f; eyeNextBlink = Random.Range(2.5f, 5f); }
                eyeBlink = Mathf.MoveTowards(eyeBlink, 0f, dt / 0.25f);
                // Drifts slowly towards the cursor.
                eyeView = Vector2.MoveTowards(eyeView, new Vector2(pointer.x, Mathf.Clamp(pointer.y, 0.4f, 0.9f)), 0.04f * dt);
                if (eyeTimer <= 0f) eyeState = EyeState.Closing;
                break;
            case EyeState.Closing:
                eyeOpen = Mathf.MoveTowards(eyeOpen, 0f, dt / 0.9f);
                if (eyeOpen <= 0f) { eyeState = EyeState.Hidden; eyeTimer = HiddenSeconds(); eyeRoot.SetActive(false); }
                break;
        }
        if (!eyeRoot.activeSelf) return;

        // The pupil points at the cursor.
        Vector2 toward = (pointer - eyeView);
        toward.x *= cam.aspect;
        Vector2 targetLook = Vector2.ClampMagnitude(toward * 3f, 1f);
        eyeLook = Vector2.Lerp(eyeLook, targetLook, 1f - Mathf.Exp(-6f * dt));

        float open = Mathf.Clamp01(eyeOpen * (1f - 0.92f * eyeBlink));
        // Size and place the eye on the screen, far in front of the camera (the scene's own objects hide it).
        float dist = Mathf.Clamp(cam.farClipPlane * 0.8f, 20f, 900f);
        float viewH = cam.orthographic ? cam.orthographicSize * 2f : 2f * dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float h = viewH * (eyeSize > 0f ? eyeSize : 0.16f);
        float w = h * 2f;
        Transform t = eyeRoot.transform;
        t.position = cam.ViewportToWorldPoint(new Vector3(eyeView.x, eyeView.y, dist));
        t.rotation = cam.transform.rotation;

        eyeWhite.localScale = new Vector3(w, Mathf.Max(0.001f, h * open), 1f);
        eyeOutline.localScale = eyeWhite.localScale;
        float ir = h * 0.72f;
        eyeIris.localScale = new Vector3(ir, Mathf.Max(0.001f, ir * open), 1f);
        eyeIris.localPosition = new Vector3(eyeLook.x * w * 0.14f, eyeLook.y * h * 0.04f * open, -0.02f * dist * 0.01f);
        eyeWhite.localPosition = Vector3.zero;
        eyeOutline.localPosition = new Vector3(0f, 0f, -0.04f * dist * 0.01f);

        float alpha = Mathf.Clamp01(open * 2.5f);
        foreach (Material m in eyeMats) { Color c = m.color; c.a = alpha; m.color = c; }
    }

    private void OnDisable()
    {
        if (eyeRoot != null) eyeRoot.SetActive(false);
        eyeArmed = false;
    }

    private void DestroyEye()
    {
        if (eyeRoot != null) Destroy(eyeRoot);
        if (eyeMats != null) foreach (Material m in eyeMats) if (m != null) Destroy(m);
        if (eyeTextures != null) foreach (Texture2D t in eyeTextures) if (t != null) Destroy(t);
    }

    private void BuildEye(SkyStyle s)
    {
        eyeRoot = new GameObject("Watcher Eye");
        eyeRoot.hideFlags = HideFlags.DontSave;
        eyeTextures = new[] { WhiteTexture(), IrisTexture(s.accentA), OutlineTexture(s.accentB) };
        eyeMats = new Material[3];
        Transform[] parts = new Transform[3];
        string[] names = { "White", "Iris", "Outline" };
        Mesh quad = EyeQuad();
        for (int i = 0; i < 3; i++)
        {
            GameObject g = new GameObject(names[i], typeof(MeshFilter), typeof(MeshRenderer));
            g.transform.SetParent(eyeRoot.transform, false);
            g.GetComponent<MeshFilter>().sharedMesh = quad;
            MeshRenderer mr = g.GetComponent<MeshRenderer>();
            Material m = new Material(PixelShaders.SpriteDefault()) { name = "Watcher " + names[i], mainTexture = eyeTextures[i] };
            m.renderQueue = 3000 + i;
            m.color = Color.white;
            mr.sharedMaterial = m;
            mr.sortingOrder = i;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            eyeMats[i] = m;
            parts[i] = g.transform;
        }
        eyeWhite = parts[0]; eyeIris = parts[1]; eyeOutline = parts[2];
        eyeRoot.SetActive(false);
    }

    private static Mesh EyeQuad()
    {
        Mesh m = new Mesh { name = "Eye Quad" };
        m.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
        m.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
        m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        m.RecalculateBounds();
        return m;
    }

    /// <summary>Half the almond's height at horizontal position nx (-1..1), as a fraction of the texture's half height (0 outside).</summary>
    private static float AlmondHeight(float nx)
    {
        float k = nx / 0.9f;
        return Mathf.Abs(k) >= 1f ? 0f : 0.8f * Mathf.Pow(1f - k * k, 0.8f);
    }

    private static Texture2D NewEyeTexture(int w, int h, string name)
    {
        return new Texture2D(w, h, TextureFormat.RGBA32, false) { name = name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
    }

    private static Texture2D WhiteTexture()
    {
        const int w = 192, h = 96;
        Texture2D tex = NewEyeTexture(w, h, "Eye White");
        Color[] px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float nx = (x + 0.5f) / w * 2f - 1f, ny = (y + 0.5f) / h * 2f - 1f;
                float eh = AlmondHeight(nx);
                float a = Mathf.Clamp01((eh - Mathf.Abs(ny)) * 20f);
                float k = eh > 0.001f ? Mathf.Pow(Mathf.Clamp01(Mathf.Abs(ny) / eh), 3f) : 1f;
                Color c = Color.Lerp(new Color(0.93f, 0.9f, 0.8f), new Color(0.55f, 0.3f, 0.25f), k);
                c.a = a;
                px[y * w + x] = c;
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    private static Texture2D IrisTexture(Color accent)
    {
        const int n = 96;
        Texture2D tex = NewEyeTexture(n, n, "Eye Iris");
        Color[] px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float nx = (x + 0.5f) / n * 2f - 1f, ny = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(nx * nx + ny * ny);
                float a = Mathf.Clamp01((1f - r) * 24f);
                Color c = r > 0.86f ? accent * 0.3f : Color.Lerp(accent * 1.15f, accent * 0.55f, r);
                float streak = Mathf.Sin(Mathf.Atan2(ny, nx) * 24f) * 0.5f + 0.5f;
                c *= 0.85f + 0.3f * streak * (1f - r);
                if (Mathf.Abs(nx) < 0.14f * (1f - ny * ny * 0.5f) && Mathf.Abs(ny) < 0.8f) c = Color.black;   // a slit pupil
                float hx = nx + 0.3f, hy = ny - 0.35f;
                if (hx * hx + hy * hy < 0.012f) c = Color.white;                                              // a glint
                c.a = a;
                px[y * n + x] = c;
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    private static Texture2D OutlineTexture(Color accent)
    {
        const int w = 192, h = 96;
        Texture2D tex = NewEyeTexture(w, h, "Eye Outline");
        Color[] px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float nx = (x + 0.5f) / w * 2f - 1f, ny = (y + 0.5f) / h * 2f - 1f;
                float eh = AlmondHeight(nx);
                float dist = eh - Mathf.Abs(ny);      // > 0 inside the lids
                float a = 0f;
                if (eh > 0f)
                {
                    a = Mathf.Clamp01(1f - Mathf.Abs(dist - 0.02f) / 0.05f);                 // the lid line
                    if (dist < 0f) a = Mathf.Max(a, Mathf.Exp(dist * 14f) * 0.45f);          // a soft glow outside it
                }
                Color c = accent * 1.2f;
                c.a = Mathf.Clamp01(a);
                px[y * w + x] = c;
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }
}
