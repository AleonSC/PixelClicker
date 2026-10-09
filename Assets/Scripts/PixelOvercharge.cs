using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Overcharge: clicking an Electric pixel (by hand) gives the auto clicker a split-second burst of extra clicks. While it lasts
/// electricity circles around the cube and a flashy "OVERCHARGE!" pops up. Needs the Auto Clicker to be bought (it can be switched
/// off in the Toggles window - the burst still happens). Bursts don't start new bursts. Added by PixelClicker.Awake.
/// </summary>
public class PixelOvercharge : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The PixelClicker whose clicks are watched. Found automatically if left empty.")]
    [SerializeField] private PixelClicker clicker;

    [Header("Burst")]
    [Tooltip("Master switch.")]
    [SerializeField] private bool enableOvercharge = true;

    [Min(1)]
    [Tooltip("How many extra auto clicks one overcharge gives.")]
    [SerializeField] private int burstClicks = 8;

    [Min(0.05f)]
    [Tooltip("How long the burst lasts (seconds): the clicks are spread evenly over it.")]
    [SerializeField] private float burstSeconds = 0.6f;

    [Tooltip("Does the burst need the Auto Clicker to be bought?")]
    [SerializeField] private bool needsAutoClicker = true;

    [Header("Electricity ring")]
    [Min(1)]
    [Tooltip("How many electricity arcs circle the cube.")]
    [SerializeField] private int orbits = 3;

    [Min(0.2f)]
    [Tooltip("Radius of the ring, in main-pixel widths.")]
    [SerializeField] private float ringRadiusPixels = 0.95f;

    [Min(0f)]
    [Tooltip("Degrees per second the arcs travel round the cube.")]
    [SerializeField] private float orbitSpeed = 620f;

    [Min(0f)]
    [Tooltip("Extra seconds the ring stays after the burst ends (it fades out).")]
    [SerializeField] private float ringFadeSeconds = 0.3f;

    [Tooltip("Colour of the bright centre of the arcs.")]
    [SerializeField] private Color coreColor = new Color(1f, 1f, 0.85f, 1f);

    [Tooltip("Colour of the glow around the arcs.")]
    [SerializeField] private Color glowColor = new Color(0.3f, 0.75f, 1f, 1f);

    [Header("Text")]
    [Tooltip("The words that pop up.")]
    [SerializeField] private string overchargeText = "Overcharge!";

    [Min(10f)]
    [Tooltip("Size of the words (canvas units).")]
    [SerializeField] private float textSize = 104f;

    [Min(0.2f)]
    [Tooltip("Seconds the words stay on screen.")]
    [SerializeField] private float textSeconds = 1.3f;

    [Min(0f)]
    [Tooltip("How far the words rise (canvas units).")]
    [SerializeField] private float textRise = 150f;

    [Tooltip("Sound played when it triggers (a code-made charge-up sweep unless you give the sound your own clips).")]
    [SerializeField] private string soundId = "overcharge";

    private bool bursting;
    private GameObject canvasRoot;
    private RectTransform canvasRect;

    private void Awake()
    {
        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
    }

    private void Start()
    {
        if (clicker != null) clicker.PixelCollected += OnCollected;
    }

    private void OnDestroy()
    {
        if (clicker != null) clicker.PixelCollected -= OnCollected;
    }

    private void OnCollected(int tierIndex, double amount, bool automatic)
    {
        if (!enableOvercharge || automatic || bursting || clicker == null) return;
        if (!clicker.IsValidTierIndex(tierIndex) || clicker.Tiers[tierIndex].type != PixelClicker.PixelType.Electric) return;

        if (needsAutoClicker)
        {
            PixelAutoClicker auto = PixelFind.First<PixelAutoClicker>();
            if (auto == null || !(auto.Running || auto.UserDisabled)) return;
        }
        StartCoroutine(Burst());
    }

    private IEnumerator Burst()
    {
        bursting = true;
        PixelAudio.Play(soundId);
        PixelStats.Count("overcharge.bursts");
        SpawnRing();
        SpawnText();

        float gap = burstSeconds / Mathf.Max(1, burstClicks);
        for (int i = 0; i < burstClicks; i++)
        {
            yield return new WaitForSeconds(gap);
            if (clicker == null) break;
            if (clicker.CubeHidden || PixelMinigame.TakeoverActive) continue; // the cube is hidden by an event: no clicks
            clicker.AutoCollect();
        }
        bursting = false;
    }

    // ------------------------------------------------------------------
    // The electricity ring
    // ------------------------------------------------------------------

    private void SpawnRing()
    {
        if (clicker.PixelTransform == null) return;
        GameObject go = new GameObject("Overcharge Ring", typeof(MeshFilter), typeof(MeshRenderer));
        PixelOverchargeRing ring = go.AddComponent<PixelOverchargeRing>();
        ring.Setup(clicker, ringRadiusPixels, orbits, orbitSpeed, burstSeconds + ringFadeSeconds, ringFadeSeconds, coreColor, glowColor);
    }

    // ------------------------------------------------------------------
    // The words
    // ------------------------------------------------------------------

    private void SpawnText()
    {
        if (canvasRoot == null)
        {
            canvasRoot = PixelUIKit.CreateCanvas("PixelOvercharge Canvas", 640, new Vector2(1920f, 1080f), false);
            canvasRoot.transform.SetParent(transform, false);
            canvasRect = canvasRoot.GetComponent<RectTransform>();
        }
        StartCoroutine(TextRoutine());
    }

    private IEnumerator TextRoutine()
    {
        TMP_Text t = PixelUIKit.CreateText(clicker.UIFont, canvasRoot.transform, "Overcharge Text", overchargeText.ToUpperInvariant(), textSize,
                                           TextAlignmentOptions.Center, FontStyles.Bold | FontStyles.Italic, Color.white);
#if UNITY_2023_1_OR_NEWER
        t.textWrappingMode = TextWrappingModes.NoWrap;
#else
        t.enableWordWrapping = false;
#endif
        t.outlineColor = new Color32(30, 120, 255, 255);
        t.outlineWidth = 0.28f;
        RectTransform r = t.rectTransform;
        r.anchorMin = r.anchorMax = Vector2.zero;
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(1400f, textSize * 1.4f);

        Vector2 start = StartPosition();
        float tilt = Random.Range(-7f, 7f);
        Color[] flash = { Color.white, new Color(1f, 0.95f, 0.35f), new Color(0.55f, 0.95f, 1f), new Color(1f, 0.7f, 0.2f) };
        float flashTimer = 0f;
        int flashIndex = 0;

        for (float e = 0f; e < textSeconds; e += Time.unscaledDeltaTime)
        {
            float k = e / textSeconds;

            // Pops in with an overshoot, settles, then drifts up and fades.
            float pop = Mathf.Clamp01(e / 0.22f);
            float scale = pop < 1f ? Mathf.LerpUnclamped(0.25f, 1.15f, 1f - (1f - pop) * (1f - pop)) : Mathf.Lerp(1.15f, 1f, Mathf.Clamp01((e - 0.22f) / 0.25f));
            scale *= 1f + 0.04f * Mathf.Sin(e * 38f);

            // Shakes hard at the start, then calms down.
            float shake = Mathf.Lerp(16f, 0f, Mathf.Clamp01(e / 0.7f));
            Vector2 jitter = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * shake;
            r.anchoredPosition = start + new Vector2(0f, textRise * Mathf.SmoothStep(0f, 1f, k)) + jitter;
            r.localScale = Vector3.one * scale;
            r.localRotation = Quaternion.Euler(0f, 0f, tilt + Random.Range(-2f, 2f) * (1f - k));

            flashTimer -= Time.unscaledDeltaTime;
            if (flashTimer <= 0f) { flashTimer = 0.045f; flashIndex = (flashIndex + 1) % flash.Length; }
            Color c = flash[flashIndex];
            c.a = 1f - Mathf.Clamp01((k - 0.7f) / 0.3f);
            t.color = c;
            yield return null;
        }
        Destroy(t.gameObject);
    }

    /// <summary>Just above the cube on screen (canvas units), kept on screen.</summary>
    private Vector2 StartPosition()
    {
        Camera cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
        Vector2 screen = new Vector2(Screen.width * 0.5f, Screen.height * 0.6f);
        if (cam != null && clicker.PixelTransform != null)
        {
            Vector3 p = cam.WorldToScreenPoint(clicker.PixelTransform.position + Vector3.up * clicker.PixelBaseSize * 0.9f);
            if (p.z > 0f) screen = new Vector2(p.x, p.y);
        }
        float toUnits = canvasRect.rect.width / Mathf.Max(1f, Screen.width);
        Vector2 pos = screen * toUnits;
        pos.x = Mathf.Clamp(pos.x, 500f, canvasRect.rect.width - 500f);
        pos.y = Mathf.Clamp(pos.y, 200f, canvasRect.rect.height - 250f);
        return pos;
    }
}

/// <summary>The arcs that circle the cube during an overcharge: a few jagged bolts travelling round tilted orbits, redrawn many times a second.</summary>
public class PixelOverchargeRing : MonoBehaviour
{
    private PixelClicker clicker;
    private float radiusPixels, speed, lifetime, fade;
    private int orbits;
    private Color core, glow;
    private float age, redraw, spin;
    private Mesh mesh;
    private readonly PixelBolts.Builder builder = new PixelBolts.Builder();
    private Quaternion[] tilts;

    public void Setup(PixelClicker owner, float radius, int orbitCount, float degreesPerSecond, float seconds, float fadeSeconds, Color coreColour, Color glowColour)
    {
        clicker = owner;
        radiusPixels = radius;
        orbits = Mathf.Max(1, orbitCount);
        speed = degreesPerSecond;
        lifetime = Mathf.Max(0.1f, seconds);
        fade = Mathf.Max(0.01f, fadeSeconds);
        core = coreColour;
        glow = glowColour;

        mesh = new Mesh { name = "Overcharge Ring" };
        mesh.MarkDynamic();
        GetComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer mr = GetComponent<MeshRenderer>();
        mr.sharedMaterial = PixelLooks.OverlayMaterial();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        tilts = new Quaternion[orbits];
        for (int i = 0; i < orbits; i++)
            tilts[i] = Quaternion.Euler(Random.Range(-70f, 70f), Random.Range(0f, 180f), Random.Range(0f, 360f));
        Follow();
        Rebuild(1f);
    }

    private void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
    }

    private void Follow()
    {
        if (clicker != null && clicker.PixelTransform != null) transform.position = clicker.PixelTransform.position;
    }

    private void Update()
    {
        age += Time.unscaledDeltaTime;
        if (age >= lifetime) { Destroy(gameObject); return; }
        Follow();
        spin += speed * Time.unscaledDeltaTime;
        redraw -= Time.unscaledDeltaTime;
        if (redraw > 0f) return;
        redraw = 0.03f;
        float alpha = Mathf.Clamp01((lifetime - age) / fade);
        Rebuild(alpha);
    }

    private void Rebuild(float alpha)
    {
        float unit = Mathf.Max(0.1f, clicker != null ? clicker.PixelBaseSize : 1f);
        float radius = radiusPixels * unit;
        builder.Clear();
        const int samples = 9;
        for (int o = 0; o < orbits; o++)
        {
            float start = spin * (o % 2 == 0 ? 1f : -1.3f) + o * (360f / orbits);
            float arc = Random.Range(70f, 120f);
            Vector3[] pts = new Vector3[samples];
            for (int s = 0; s < samples; s++)
            {
                float a = (start + arc * s / (samples - 1)) * Mathf.Deg2Rad;
                Vector3 p = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius;
                if (s > 0 && s < samples - 1) p += Random.insideUnitSphere * (radius * 0.07f);
                pts[s] = tilts[o] * p;
            }
            PixelBolts.Strokes(builder, pts, unit * 0.05f, unit * 0.16f, true, core, glow, alpha);
        }
        builder.Apply(mesh, new Bounds(Vector3.zero, Vector3.one * radius * 3f));
    }
}
