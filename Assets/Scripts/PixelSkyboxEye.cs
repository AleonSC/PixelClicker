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
    private Transform eyeWhite, eyeIris, eyeOutline, eyeHalo, eyePupil;
    private float[] eyeAlphaScale;
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
        UpdateRain(cam);
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

        float tNow = Time.unscaledTime;
        eyeWhite.localScale = new Vector3(w, Mathf.Max(0.001f, h * open), 1f);
        eyeOutline.localScale = new Vector3(w * 1.12f, Mathf.Max(0.001f, h * 1.12f * open), 1f);   // (a little bigger: it carries the lashes)
        float ir = h * 0.74f;
        eyeIris.localScale = new Vector3(ir, Mathf.Max(0.001f, ir * open), 1f);
        Vector3 irisPos = new Vector3(eyeLook.x * w * 0.14f, eyeLook.y * h * 0.04f * open, -0.02f * dist * 0.01f);
        eyeIris.localPosition = irisPos;
        // The slit pupil breathes slowly, and narrows when the eye stares right at the cursor.
        float stare = 1f - Mathf.Clamp01(toward.magnitude * 2.5f);
        float dilation = (0.75f + 0.35f * Mathf.Sin(tNow * 1.6f)) * Mathf.Lerp(1f, 0.6f, stare);
        eyePupil.localScale = new Vector3(Mathf.Max(0.001f, ir * 0.26f * dilation), Mathf.Max(0.001f, ir * open * 0.98f), 1f);
        eyePupil.localPosition = irisPos + new Vector3(eyeLook.x * w * 0.02f, 0f, -0.01f * dist * 0.01f);
        eyeWhite.localPosition = Vector3.zero;
        eyeOutline.localPosition = new Vector3(0f, 0f, -0.04f * dist * 0.01f);
        // The glow behind the eye pulses like a heartbeat.
        float beat = 1f + 0.07f * Mathf.Sin(tNow * 2.4f) + 0.04f * Mathf.Sin(tNow * 5.1f);
        eyeHalo.localScale = new Vector3(w * 2.2f * beat, Mathf.Max(0.001f, h * 3.3f * beat * Mathf.Lerp(0.35f, 1f, open)), 1f);
        eyeHalo.localPosition = new Vector3(0f, 0f, 0.05f * dist * 0.01f);

        float alpha = Mathf.Clamp01(open * 2.5f);
        for (int i = 0; i < eyeMats.Length; i++) { Color c = eyeMats[i].color; c.a = alpha * eyeAlphaScale[i]; eyeMats[i].color = c; }
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
        // back to front: glow, sclera, iris, pupil, lid line + lashes
        eyeTextures = new[] { HaloTexture(s.accentB, s.accentA), WhiteTexture(), IrisTexture(s.accentA, s.accentB), PupilTexture(s.accentB), OutlineTexture(s.accentB, s.accentA) };
        eyeAlphaScale = new[] { 0.55f, 1f, 1f, 1f, 1f };
        string[] names = { "Halo", "White", "Iris", "Pupil", "Outline" };
        eyeMats = new Material[names.Length];
        Transform[] parts = new Transform[names.Length];
        Mesh quad = EyeQuad();
        for (int i = 0; i < names.Length; i++)
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
        eyeHalo = parts[0]; eyeWhite = parts[1]; eyeIris = parts[2]; eyePupil = parts[3]; eyeOutline = parts[4];
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

    /// <summary>A soft glow round the whole eye.</summary>
    private static Texture2D HaloTexture(Color outer, Color inner)
    {
        const int n = 128;
        Texture2D tex = NewEyeTexture(n, n, "Eye Halo");
        Color[] px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float nx = (x + 0.5f) / n * 2f - 1f, ny = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(nx * nx + ny * ny);
                float a = Mathf.Pow(Mathf.Clamp01(1f - r), 2.2f);
                Color c = Color.Lerp(inner, outer, Mathf.Clamp01(r * 1.2f)) * 1.1f;
                c.a = a * 0.8f;
                px[y * n + x] = c;
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    /// <summary>The white of the eye: shaded darker and redder towards the lids, with bloodshot veins creeping in from the corners.</summary>
    private static Texture2D WhiteTexture()
    {
        const int w = 256, h = 128;
        Texture2D tex = NewEyeTexture(w, h, "Eye White");
        Color[] px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float nx = (x + 0.5f) / w * 2f - 1f, ny = (y + 0.5f) / h * 2f - 1f;
                float eh = AlmondHeight(nx);
                float a = Mathf.Clamp01((eh - Mathf.Abs(ny)) * 20f);
                float k = eh > 0.001f ? Mathf.Pow(Mathf.Clamp01(Mathf.Abs(ny) / eh), 3f) : 1f;
                Color c = Color.Lerp(new Color(0.95f, 0.91f, 0.82f), new Color(0.6f, 0.3f, 0.26f), k);
                // Veins: wavy lines running from the corners towards the middle, fading out before they reach it.
                float veins = 0f;
                for (int v = 0; v < 9; v++)
                {
                    float side = (v & 1) == 0 ? -1f : 1f;
                    float start = side * 0.92f;
                    float along = (nx - start) / (-side * 1.2f);                 // 0 at the corner, 1 a way in
                    if (along < 0f || along > 1f) continue;
                    float baseY = (v / 2 - 2) * 0.17f * (1f - along * 0.6f);
                    float wave = Mathf.Sin(along * (7f + v) + v * 1.7f) * 0.05f * along;
                    float dist = Mathf.Abs(ny - (baseY + wave));
                    veins = Mathf.Max(veins, Mathf.Clamp01(1f - dist / 0.014f) * (1f - along) * 0.85f);
                }
                c = Color.Lerp(c, new Color(0.75f, 0.08f, 0.08f), veins);
                c.a = a;
                px[y * w + x] = c;
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    /// <summary>The iris: a dark limbal ring, glowing radial fibres, a bright ring round the pupil and soft glints. (The slit pupil is its own piece.)</summary>
    private static Texture2D IrisTexture(Color accent, Color hot)
    {
        const int n = 128;
        Texture2D tex = NewEyeTexture(n, n, "Eye Iris");
        Color[] px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float nx = (x + 0.5f) / n * 2f - 1f, ny = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(nx * nx + ny * ny);
                float a = Mathf.Clamp01((1f - r) * 30f);
                float ang = Mathf.Atan2(ny, nx);
                Color c = Color.Lerp(hot * 1.2f, accent * 0.9f, Mathf.Clamp01(r * 1.4f - 0.1f));          // hot near the middle, the iris colour outside
                float fibre = 0.5f + 0.5f * Mathf.Sin(ang * 36f + Mathf.Sin(ang * 5f + r * 6f) * 1.4f);
                float fibre2 = 0.5f + 0.5f * Mathf.Sin(ang * 71f - r * 9f);
                c *= 0.55f + 0.55f * fibre * (1f - r * 0.4f) + 0.2f * fibre2;
                float collarette = Mathf.Exp(-Mathf.Pow((r - 0.4f) / 0.07f, 2f));                         // a bright ring around the pupil
                c = Color.Lerp(c, Color.Lerp(accent, Color.white, 0.5f), collarette * 0.55f);
                float limbal = Mathf.Clamp01((r - 0.78f) / 0.2f);                                          // dark edge
                c = Color.Lerp(c, accent * 0.12f, limbal * 0.9f);
                float hx = nx + 0.32f, hy = ny - 0.36f;
                float glint = Mathf.Exp(-(hx * hx + hy * hy) / 0.006f);
                float glint2 = Mathf.Exp(-((nx - 0.28f) * (nx - 0.28f) + (ny + 0.3f) * (ny + 0.3f)) / 0.003f) * 0.6f;
                c = Color.Lerp(c, Color.white, Mathf.Clamp01(glint + glint2));
                c.a = a;
                px[y * n + x] = c;
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    /// <summary>A vertical slit pupil with pointed ends, black with a faint glowing rim.</summary>
    private static Texture2D PupilTexture(Color rim)
    {
        const int w = 64, h = 128;
        Texture2D tex = NewEyeTexture(w, h, "Eye Pupil");
        Color[] px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float nx = (x + 0.5f) / w * 2f - 1f, ny = (y + 0.5f) / h * 2f - 1f;
                float half = Mathf.Pow(Mathf.Clamp01(1f - ny * ny), 0.8f);      // a lens: widest in the middle, pointed at the ends
                float d = half - Mathf.Abs(nx);
                float a = Mathf.Clamp01(d * 12f);
                Color c = Color.Lerp(rim * 0.9f, Color.black, Mathf.Clamp01(d * 5f));
                c.a = a;
                px[y * w + x] = c;
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    /// <summary>The lid lines with a soft glow outside them and tapering lashes along both lids.</summary>
    private static Texture2D OutlineTexture(Color accent, Color lash)
    {
        const int w = 256, h = 128;
        Texture2D tex = NewEyeTexture(w, h, "Eye Outline");
        Color[] px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                // (the picture is 1.12x the eye: map back to the eye's own coordinates)
                float nx = ((x + 0.5f) / w * 2f - 1f) * 1.12f, ny = ((y + 0.5f) / h * 2f - 1f) * 1.12f;
                float eh = AlmondHeight(nx);
                float dist = eh - Mathf.Abs(ny);      // > 0 inside the lids
                float a = 0f;
                Color c = accent * 1.25f;
                if (eh > 0f)
                {
                    a = Mathf.Clamp01(1f - Mathf.Abs(dist - 0.025f) / 0.06f);                 // the lid line, thicker than before
                    if (dist < 0f) a = Mathf.Max(a, Mathf.Exp(dist * 12f) * 0.5f);            // a soft glow outside it
                }
                // Lashes: short tapering spikes fanning outwards from both lids.
                if (dist < 0f && Mathf.Abs(nx) < 0.82f)
                {
                    float spacing = 0.13f;
                    float cell = nx / spacing;
                    float f = cell - Mathf.Floor(cell) - 0.5f;                                // -0.5..0.5 across one lash
                    float len = 0.15f * (0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Mathf.Floor(cell) * 2.4f))) * (1f - Mathf.Abs(nx) * 0.35f);
                    float outside = -dist;
                    float lean = nx * 0.12f;                                                  // lashes fan outwards
                    float width = 0.07f * Mathf.Clamp01(1f - outside / len);
                    if (outside < len && Mathf.Abs(f * spacing - lean * outside) < width)
                    {
                        a = Mathf.Max(a, 0.95f);
                        c = lash * 1.2f;
                    }
                }
                c.a = Mathf.Clamp01(a);
                px[y * w + x] = c;
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }
}
