// ---- System notes ----
// PixelHazardTell.cs
//   TELLS that the block on the cube is a HAZARD (component added by PixelClicker.Awake; applies to every hazard block - PixelClicker.IsHazard: Hornet Nest, Pest, Gas Pocket, Jellyfish,
//   Mold - while it sits on the cube): a pulsing warning-orange glow shell round the cube, a bobbing red warning triangle with a '!' floating above it (a billboard), and a warning
//   sound (hazard_warn, a two-tone warble) the moment the hazard appears, plus an angry buzz (hornet_buzz) for the Hornet Nest. Everything pulses FASTER as the hazard's time on the
//   cube runs out (it leaves by itself after hazardSeconds), so the pulse also tells how long is left. Switch off with disableTells; colours / sizes are serialized.
//   Sounds (code-made): PixelHazardSynth, hooked into PixelAudio.SynthFor.
// ---------------------------------------------------
using UnityEngine;

public class PixelHazardTell : MonoBehaviour
{
    [Tooltip("Turns all the hazard tells off.")]
    [SerializeField] private bool disableTells = false;

    [Tooltip("Colour of the glow shell and the warning sign.")]
    [SerializeField] private Color warningColor = new Color(1f, 0.38f, 0.08f, 1f);

    [Min(0f)]
    [Tooltip("How strong the glow shell is (0 = off, 1 = the default look). 0 = the coded default (1).")]
    [SerializeField] private float glowStrength = 0f;

    [Min(0f)]
    [Tooltip("How big the warning sign is, in cube widths. 0 = the coded default (0.6).")]
    [SerializeField] private float signSize = 0f;

    private PixelClicker clicker;
    private Renderer shell;
    private Material shellMat;
    private SpriteRenderer sign;
    private float lastLiveSince = -1f;
    private static Sprite signSprite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { signSprite = null; }

    private void Awake() { clicker = GetComponent<PixelClicker>(); }

    private void OnDisable() { Hide(); }

    private void LateUpdate()
    {
        if (disableTells || clicker == null || !clicker.TryGetLiveHazard(out PixelClicker.PixelType type, out float remaining, out float since)) { Hide(); return; }
        Transform cube = clicker.PixelTransform;
        if (cube == null || clicker.CubeHidden) { Hide(); return; }

        float size = clicker.PixelBaseSize;
        float time = Time.unscaledTime;
        float urgency = 1f - Mathf.Clamp01(remaining);                 // 0 = just appeared, 1 = about to leave
        float rate = 4f + urgency * 10f;                                // the pulse quickens as it runs out
        float pulse = 0.5f + 0.5f * Mathf.Sin(time * rate);

        // The moment a new hazard appears: the warning sound.
        if (!Mathf.Approximately(since, lastLiveSince))
        {
            lastLiveSince = since;
            PixelAudio.Play("hazard_warn");
            if (type == PixelClicker.PixelType.HornetNest) PixelAudio.Play("hornet_buzz");
        }

        // The glow shell.
        if (shell == null)
        {
            shell = PixelMold.MakeShell(cube, 1.14f, out shellMat);
            shell.gameObject.name = "Hazard Glow";
        }
        else if (shell.transform.parent != cube) shell.transform.SetParent(cube, false);
        shell.gameObject.SetActive(true);
        Color g = warningColor;
        g.a = (0.08f + 0.2f * pulse) * (glowStrength > 0f ? glowStrength : 1f);
        shellMat.color = g;

        // The warning sign: a red triangle with a '!' bobbing above the cube, always facing the camera.
        if (sign == null)
        {
            GameObject go = new GameObject("Hazard Sign");
            sign = go.AddComponent<SpriteRenderer>();
            if (signSprite == null) signSprite = BuildSignSprite();
            sign.sprite = signSprite;
            sign.sortingOrder = 50;
        }
        sign.gameObject.SetActive(true);
        float scale = size * (signSize > 0f ? signSize : 0.6f) / 1.28f * (1f + 0.12f * pulse);
        sign.transform.localScale = Vector3.one * scale;
        sign.transform.position = cube.position + Vector3.up * (size * 1.0f + 0.08f * size * Mathf.Sin(time * 3f));
        Vector3 camPos;
        if (PixelFirstPerson.TryGetViewRay(out Ray ray)) camPos = ray.origin;
        else { Camera cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main; camPos = cam != null ? cam.transform.position : cube.position + Vector3.back; }
        Vector3 away = sign.transform.position - camPos;
        if (away.sqrMagnitude > 0.0001f) sign.transform.rotation = Quaternion.LookRotation(away.normalized);
        Color sc = Color.Lerp(Color.white, warningColor, 0.25f * pulse);
        sc.a = 0.9f;
        sign.color = sc;
    }

    private void Hide()
    {
        if (shell != null && shell.gameObject.activeSelf) shell.gameObject.SetActive(false);
        if (sign != null && sign.gameObject.activeSelf) sign.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (shell != null) Destroy(shell.gameObject);
        if (sign != null) Destroy(sign.gameObject);
        if (shellMat != null) Destroy(shellMat);
    }

    /// <summary>A warning triangle (red-orange, dark outline) with a white '!' in it, drawn in code.</summary>
    private static Sprite BuildSignSprite()
    {
        const int n = 128;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        Color32[] px = new Color32[n * n];
        Color fill = new Color(0.95f, 0.22f, 0.12f, 1f), edge = new Color(0.25f, 0.04f, 0.02f, 1f);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n, v = (y + 0.5f) / n;                       // 0..1, v up
                // Triangle: apex at the top middle, base along the bottom.
                float halfWidth = Mathf.Lerp(0.5f, 0f, (v - 0.08f) / 0.84f);
                float d = Mathf.Abs(u - 0.5f) - halfWidth;                         // < 0 inside horizontally
                float inside = Mathf.Min(-d, Mathf.Min(v - 0.08f, 0.92f - v)) * n;   // distance in from the edge, in pixels (approx)
                Color c = Color.clear;
                if (inside > 0f) c = inside < 6f ? edge : fill;
                // The '!': a bar and a dot, white.
                bool bar = Mathf.Abs(u - 0.5f) < 0.045f && v > 0.34f && v < 0.64f;
                bool dot = (u - 0.5f) * (u - 0.5f) + (v - 0.25f) * (v - 0.25f) < 0.05f * 0.05f;
                if (inside > 8f && (bar || dot)) c = Color.white;
                px[y * n + x] = c;
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }
}

/// <summary>Code-made hazard sounds: hazard_warn (a two-tone warble) and hornet_buzz (an angry droning buzz).</summary>
public static class PixelHazardSynth
{
    private const int Rate = 22050;

    public static AudioClip[] For(string id)
    {
        if (id == "hazard_warn") return new[] { Warn() };
        if (id == "hornet_buzz") return new[] { Buzz() };
        return null;
    }

    private static AudioClip Finish(string name, float[] data, float level)
    {
        float peak = 0.0001f;
        for (int i = 0; i < data.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < data.Length; i++) data[i] = data[i] / peak * level;
        AudioClip clip = AudioClip.Create(name, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static AudioClip Warn()
    {
        int n = (int)(Rate * 0.42f);
        float[] data = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float sec = i / (float)Rate;
            float hz = (Mathf.FloorToInt(sec / 0.105f) % 2 == 0) ? 880f : 660f;   // two tones, four beeps
            phase += hz / Rate;
            float tri = Mathf.Abs(Mathf.Repeat(phase, 1f) * 2f - 1f) * 2f - 1f;
            float gate = Mathf.Clamp01(Mathf.Repeat(sec, 0.105f) / 0.006f) * Mathf.Clamp01((0.105f - Mathf.Repeat(sec, 0.105f)) / 0.01f);
            data[i] = tri * gate * Mathf.Lerp(1f, 0.5f, sec / 0.42f);
        }
        return Finish("Hazard Warn", data, 0.5f);
    }

    private static AudioClip Buzz()
    {
        int n = (int)(Rate * 0.6f);
        float[] data = new float[n];
        float phase = 0f;
        System.Random rng = new System.Random(31);
        for (int i = 0; i < n; i++)
        {
            float sec = i / (float)Rate;
            float hz = 190f + 35f * Mathf.Sin(sec * 38f) + 20f * Mathf.Sin(sec * 91f);   // wobbling drone
            phase += hz / Rate;
            float saw = Mathf.Repeat(phase, 1f) * 2f - 1f;
            float noise = ((float)rng.NextDouble() * 2f - 1f) * 0.25f;
            float env = Mathf.Clamp01(sec / 0.05f) * Mathf.Clamp01((0.6f - sec) / 0.2f);
            data[i] = (saw * 0.7f + noise) * env * (0.7f + 0.3f * Mathf.Sin(sec * 60f));
        }
        return Finish("Hornet Buzz", data, 0.45f);
    }
}
