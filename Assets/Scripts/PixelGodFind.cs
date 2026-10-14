// ---- System notes ----
// PixelGodFind.cs
//   The finds of the three Egyptian God Cubes (component added by PixelClicker.Awake; PixelGodFind.Play(tierIndex, worldPos), queued; called from PixelClicker.CollectInternal when a God Cube pays out).
//   The cubes (PixelType.GodSlifer / GodRa / GodObelisk, PixelClicker.IsGodCube, tiers made by EnsureGodCubes, spawn weight godCubeSpawnWeight = 0.000001, one of each: CanSpawn is false and
//   AddCurrency ignores a second one) have their OWN show each, all drawn in UI space on a canvas that blocks clicks (sorting 691, unscaled time, Escape skips to the backpack like the Dragon Cube show):
//     SLIFER - a storm: dark clouds, three lightning strikes (flash + jagged bolts), a long red lightning serpent that flies across the screen in a figure-eight with crackling arcs, then coils into the cube
//       in a final blast.
//     RA - a golden sphere rises and grows, bursts into a sun (rays, whiteout), then golden wings (two fans of feathers) beat around the cube to the rhythm of the sound while feathers fall, and fold in with a golden shockwave.
//     OBELISK - the screen shakes harder and harder as rubble falls, a giant blue fist lowers into view, cocks back and SLAMS the screen: flash, crack lines radiate, shockwaves, debris; the cube rises from the crater.
//   Then the cube hops into the backpack like the Dragon Cube's. Sounds god_slifer / god_ra / god_obelisk (code-made PixelGodSynth, timed to the shows). Settings: disableShow, showSpeed, darkness, cubeSize.
//   The cube picture is a hidden studio camera -> RenderTexture (a trimmed copy of PixelDragonFind's studio; the two could share one helper).
// ---------------------------------------------------
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PixelGodFind : MonoBehaviour
{
    [Header("General")]
    [Tooltip("Turn the shows off (the cube is still added, only the animation is skipped).")]
    [SerializeField] private bool disableShow = false;

    [Min(0f)]
    [Tooltip("Speed of the shows (1 = normal, 2 = twice as fast). 0 = 1.")]
    [SerializeField] private float showSpeed = 0f;

    [Header("Look")]
    [Range(0f, 1f)]
    [Tooltip("How dark the screen gets (0 = not at all, 1 = black).")]
    [SerializeField] private float darkness = 0.9f;
    [Tooltip("Size of the cube at its biggest, in canvas units (the canvas is 1080 high).")]
    [SerializeField] private float cubeSize = 520f;
    [Tooltip("Heading text. {0} = the god's name.")]
    [SerializeField] private string foundFormat = "You found {0}!";
    [Tooltip("Heading text for the Infinity Cubelette (made with the Gauntlet). {0} = its name.")]
    [SerializeField] private string forgedFormat = "You forged the {0}!";
    [Tooltip("Text under the heading while the cube goes into the backpack.")]
    [SerializeField] private string stashedText = "Only one of each exists. It's yours now";
    [Tooltip("Size of the backpack icon (canvas units).")]
    [SerializeField] private float backpackSize = 130f;
    [Tooltip("Distance of the backpack icon from the right edge and the bottom bar (canvas units).")]
    [SerializeField] private Vector2 backpackMargin = new Vector2(40f, 40f);

    private enum God { Slifer, Ra, Obelisk, Stone, Cubelette }
    private struct Request { public int tier; public Vector2 screenPos; }
    private struct Bit { public RectTransform rect; public Image image; public Vector2 pos, vel; public float life, maxLife, size, spin, gravity; public bool alive; }
    private struct Ring { public RectTransform rect; public Image image; public Vector2 pos; public float age, life, from, to; public Color color; public bool alive; }

    private static PixelGodFind instance;
    private readonly Queue<Request> queue = new Queue<Request>();
    private bool playing, skipRequested;
    private PixelClicker clicker;

    private GameObject canvasRoot, studioRoot;
    private RectTransform canvasRect, stage, cubeRect;
    private Image dim, flashImage, backpack;
    private RawImage cubeImage;
    private TMP_Text title, subtitle;
    private Camera studioCam;
    private RenderTexture rt;
    private Transform pivot;

    private Image[] clouds, segs, feathers, fist, lines;
    private Image sun, sphere, glowBehind;
    private Bit[] bits;
    private Ring[] rings;
    private int lineCount;
    private static Sprite softSprite, squareSprite, ringSprite, sunSprite, featherSprite;

    // show state
    private float flash, shake, stormAlpha;
    private bool[] fired;
    private int stoneIdx;
    private Vector2 shakeOffset;
    private readonly List<Vector2[]> cracks = new List<Vector2[]>();
    private Vector2 impactPoint;
    private float impactTime = -99f;
    private System.Random boltRng;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; softSprite = squareSprite = ringSprite = sunSprite = featherSprite = null; }

    private void Awake()
    {
        instance = this;
        PixelWindows.Register(this, 165, () => playing, () => skipRequested = true);   // Escape skips the show (the cube is already yours)
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        PixelAudio.SetMusicCue("God find", false);
        PixelWindows.Unregister(this);
        Cleanup();
    }

    /// <summary>Starts (or queues) the show of this God Cube tier.</summary>
    public static void Play(int tierIndex, Vector3 worldPosition)
    {
        if (instance == null || instance.disableShow) return;
        instance.Enqueue(tierIndex, worldPosition);
    }

    /// <summary>Starts (or queues) the show of a tier at a screen position (the Gauntlet's forge: the Cubelette is not collected from the cube).</summary>
    public static void PlayAtScreen(int tierIndex, Vector2 screenPosition)
    {
        if (instance == null || instance.disableShow) return;
        if (instance.clicker == null) instance.clicker = PixelFind.First<PixelClicker>();
        if (instance.clicker == null || !instance.clicker.IsValidTierIndex(tierIndex)) return;
        instance.queue.Enqueue(new Request { tier = tierIndex, screenPos = screenPosition });
        if (!instance.playing) instance.StartCoroutine(instance.Show());
    }

    private void Enqueue(int tierIndex, Vector3 worldPosition)
    {
        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
        if (clicker == null || !clicker.IsValidTierIndex(tierIndex)) return;
        Camera cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
        Vector2 screen = cam != null ? (Vector2)cam.WorldToScreenPoint(worldPosition) : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        queue.Enqueue(new Request { tier = tierIndex, screenPos = screen });
        if (!playing) StartCoroutine(Show());
    }

    private System.Collections.IEnumerator Show()
    {
        playing = true;
        while (queue.Count > 0) yield return PlayOne(queue.Dequeue());
        playing = false;
    }

    private static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
    private static float EaseOut(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t) * (1f - t); }
    private float Speed => showSpeed > 0f ? showSpeed : 1f;

    // ------------------------------------------------------------------
    // The show (one coroutine; each god draws its own main part)
    // ------------------------------------------------------------------

    private System.Collections.IEnumerator PlayOne(Request req)
    {
        Build(req.tier);
        if (cubeImage == null) { Cleanup(); yield break; }
        yield return null;   // the canvas scaler sets the canvas size on the first frame

        PixelClicker.PixelType type = clicker.Tiers[req.tier].type;
        stoneIdx = PixelClicker.IsInfinityStone(type) ? (int)type - (int)PixelClicker.PixelType.StoneSpace : -1;
        God god = type == PixelClicker.PixelType.GodSlifer ? God.Slifer : type == PixelClicker.PixelType.GodRa ? God.Ra : type == PixelClicker.PixelType.GodObelisk ? God.Obelisk
                : type == PixelClicker.PixelType.InfinityCubelette ? God.Cubelette : God.Stone;
        float mainT = god == God.Slifer ? 5.9f : god == God.Ra ? 5.2f : god == God.Obelisk ? 5.5f : god == God.Stone ? 5.0f : 6.4f;
        const float stashT = 1.0f, fadeT = 0.5f;
        float total = mainT + stashT;

        Vector2 size = canvasRect.rect.size;
        Vector2 start = new Vector2(Mathf.Clamp(req.screenPos.x / Screen.width * size.x, 0f, size.x), Mathf.Clamp(req.screenPos.y / Screen.height * size.y, 0f, size.y));
        Vector2 centre = size * 0.5f + new Vector2(0f, size.y * 0.03f);
        float bar = PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f;
        Vector2 pack = new Vector2(size.x - backpackMargin.x - backpackSize * 0.5f, bar + backpackMargin.y + backpackSize * 0.5f);
        backpack.rectTransform.anchoredPosition = pack;

        Color tint, dimTint;
        if (god == God.Stone) { tint = Color.Lerp(PixelInfinity.StoneColor(stoneIdx), Color.white, 0.25f); dimTint = PixelInfinity.StoneColor(stoneIdx) * 0.12f; dimTint.a = 1f; }
        else if (god == God.Cubelette) { tint = new Color(1f, 0.93f, 0.6f, 1f); dimTint = new Color(0.06f, 0.05f, 0.02f, 1f); }
        else
        {
            tint = god == God.Slifer ? new Color(1f, 0.35f, 0.25f, 1f) : god == God.Ra ? new Color(1f, 0.85f, 0.35f, 1f) : new Color(0.5f, 0.78f, 1f, 1f);
            dimTint = god == God.Slifer ? new Color(0.09f, 0f, 0.02f, 1f) : god == God.Ra ? new Color(0.07f, 0.03f, 0f, 1f) : new Color(0f, 0.02f, 0.09f, 1f);
        }
        title.text = string.Format(god == God.Cubelette ? forgedFormat : foundFormat, clicker.Tiers[req.tier].displayName);
        title.color = tint; subtitle.text = "";
        PixelAudio.Play(god == God.Slifer ? "god_slifer" : god == God.Ra ? "god_ra" : god == God.Obelisk ? "god_obelisk" : god == God.Cubelette ? "stone_cubelette" : PixelInfinity.StoneSoundId(stoneIdx));

        PixelAudio.SetMusicCue("God find", true);   // a music cue (if it has music) takes over for the show
        flash = 0f; shake = 0f; stormAlpha = 0f; fired = new bool[10]; cracks.Clear(); impactTime = -99f;
        boltRng = new System.Random(Random.Range(0, 100000));

        float t = 0f, bump = 0f;
        Vector2 cubePos = start; float cubeScale = cubeSize * 0.12f; Vector3 euler = Vector3.zero;
        Vector2 lastPos = cubePos; float lastScale = cubeScale;
        bool stashed = false;

        while (t < total + fadeT)
        {
            float dt = Time.unscaledDeltaTime * Speed;
            if (skipRequested)
            {
                skipRequested = false;
                queue.Clear();
                if (t < mainT) t = mainT;
            }
            t += dt;
            lineCount = 0;
            float darkAmount = 1f, packAlpha = 0f, titleAlpha = 0f;
            float cubeAlpha = 1f;
            flash = Mathf.Max(0f, flash - dt * 4.5f);
            shake = Mathf.Max(0f, shake - dt * 22f);

            if (t < mainT)
            {
                titleAlpha = Smooth((t - 0.2f) / 0.6f) * Smooth((mainT + 0.6f - t) / 0.6f);
                darkAmount = Smooth(t / 0.9f);
                if (god == God.Slifer) DrawSlifer(t, dt, size, centre, start, ref cubePos, ref cubeScale, ref euler);
                else if (god == God.Ra) DrawRa(t, dt, size, centre, start, ref cubePos, ref cubeScale, ref euler);
                else if (god == God.Stone) DrawStone(stoneIdx, t, dt, size, centre, start, ref cubePos, ref cubeScale, ref euler);
                else if (god == God.Cubelette) DrawCubelette(t, dt, size, centre, start, ref cubePos, ref cubeScale, ref euler);
                else DrawObelisk(t, dt, size, centre, start, ref cubePos, ref cubeScale, ref euler);
                lastPos = cubePos; lastScale = cubeScale;
            }
            else if (t < total)
            {
                float d = (t - mainT) / stashT;
                float k = d * d * (3f - 2f * d);
                cubePos = Vector2.Lerp(lastPos, pack, k) + new Vector2(0f, Mathf.Sin(k * Mathf.PI) * size.y * 0.14f);
                cubeScale = Mathf.Lerp(Mathf.Max(lastScale, cubeSize * 0.5f), backpackSize * 0.35f, k);
                euler = new Vector3(20f + 360f * k, 700f + 720f * k, 0f);
                packAlpha = 1f; titleAlpha = 1f - Smooth(d * 1.4f); darkAmount = 1f;
                subtitle.text = d > 0.25f ? stashedText : "";
                HideScene(1f - Smooth(d * 2f));
                if (Random.value < 0.6f) SpawnBit(softSprite, cubePos + Random.insideUnitCircle * cubeScale * 0.4f, Random.insideUnitCircle * 90f, 0.6f, 30f, tint, 0f);
            }
            else
            {
                if (!stashed) { stashed = true; for (int i = 0; i < 40; i++) SpawnBit(softSprite, pack, Random.insideUnitCircle.normalized * Random.Range(90f, 380f), Random.Range(0.6f, 1.2f), Random.Range(14f, 40f), tint, 0f); PixelAudio.Play("dragon_stash"); bump = 1f; }
                float f = (t - total) / fadeT;
                darkAmount = 1f - Smooth(f); packAlpha = 1f - Smooth(f * 1.2f); titleAlpha = 0f;
                cubeScale = 0f; cubePos = pack;
                HideScene(0f);
            }

            bump = Mathf.Max(0f, bump - dt * 3.5f);
            float sh = shake;
            shakeOffset = sh > 0.01f ? new Vector2(Random.Range(-sh, sh), Random.Range(-sh, sh)) : Vector2.zero;
            stage.anchoredPosition = shakeOffset;
            ApplyFrame(cubePos + shakeOffset, cubeScale, euler, dimTint, darkAmount, packAlpha, titleAlpha, bump, cubeAlpha, tint);
            UpdateBits(dt);
            UpdateRings(dt);
            for (int i = lineCount; i < lines.Length; i++) if (lines[i].enabled) lines[i].enabled = false;
            yield return null;
        }

        PixelAudio.SetMusicCue("God find", false);
        float tail = 0.5f;
        while (tail > 0f) { tail -= Time.unscaledDeltaTime; UpdateBits(Time.unscaledDeltaTime); UpdateRings(Time.unscaledDeltaTime); yield return null; }
        Cleanup();
    }

    /// <summary>Fades out everything the god's show drew on the stage (the show is over, the cube is flying to the backpack).</summary>
    private void HideScene(float alpha)
    {
        CanvasGroup g = stage.GetComponent<CanvasGroup>();
        if (g != null) g.alpha = alpha;
    }

    private void ApplyFrame(Vector2 pos, float scale, Vector3 euler, Color dimTint, float darkAmount, float packAlpha, float titleAlpha, float bump, float cubeAlpha, Color tint)
    {
        Color c = dimTint; c.a = darkness * darkAmount; dim.color = c;
        c = backpack.color; c.a = packAlpha; backpack.color = c;
        backpack.rectTransform.localScale = Vector3.one * (1f + 0.35f * bump);
        title.alpha = titleAlpha; subtitle.alpha = titleAlpha;
        Color f = Color.Lerp(Color.white, tint, 0.35f); f.a = Mathf.Clamp01(flash) * 0.85f; flashImage.color = f;

        bool show = scale > 1f;
        if (cubeImage.enabled != show) cubeImage.enabled = show;
        if (show)
        {
            cubeImage.color = new Color(1f, 1f, 1f, cubeAlpha);
            cubeRect.anchoredPosition = pos;
            cubeRect.sizeDelta = new Vector2(scale, scale);
            pivot.localRotation = Quaternion.Euler(euler);
            studioCam.Render();
        }
    }

    // ------------------------------------------------------------------
    // SLIFER: the storm and the lightning serpent
    // ------------------------------------------------------------------

    private static readonly float[] StrikeTimes = { 0.45f, 0.95f, 1.35f, 2.9f, 4.0f };

    private Vector2 SerpentHead(float u, Vector2 size, Vector2 centre)
    {
        Vector2 p = centre + new Vector2(Mathf.Sin(u * 1.0f) * size.x * 0.40f, Mathf.Sin(u * 1.45f + 0.8f) * size.y * 0.30f + Mathf.Cos(u * 0.6f) * size.y * 0.08f);
        if (u < 0f) p += new Vector2(-1f, 0.2f) * (-u) * 900f;   // it comes in from the left
        return p;
    }

    private void DrawSlifer(float t, float dt, Vector2 size, Vector2 centre, Vector2 start, ref Vector2 cubePos, ref float cubeScale, ref Vector3 euler)
    {
        // Storm clouds.
        stormAlpha = Smooth(t / 0.8f) * (1f - Smooth((t - 4.9f) / 0.9f));
        for (int i = 0; i < clouds.Length; i++)
        {
            float x = size.x * (0.1f + 0.8f * ((i * 0.37f) % 1f)) + Mathf.Sin(t * 0.35f + i) * 90f;
            float y = size.y * (0.78f + 0.2f * ((i * 0.53f) % 1f)) + Mathf.Cos(t * 0.3f + i * 2f) * 30f;
            Put(clouds[i], new Vector2(x, y), new Vector2(900f, 520f), 0f, new Color(0.16f, 0.03f, 0.05f, 0.62f * stormAlpha));
        }

        // Lightning strikes: a flash and jagged bolts from the clouds to the middle.
        for (int s = 0; s < StrikeTimes.Length; s++)
        {
            if (!fired[s] && t >= StrikeTimes[s]) { fired[s] = true; flash = 1f; shake = Mathf.Max(shake, 12f); }
            float since = t - StrikeTimes[s];
            if (since >= 0f && since < 0.2f)
            {
                System.Random r = new System.Random(s * 977 + (int)(since * 40f));
                Vector2 top = new Vector2(size.x * (0.2f + 0.6f * (float)r.NextDouble()), size.y + 20f);
                Bolt(top, centre + new Vector2(((float)r.NextDouble() - 0.5f) * 240f, -size.y * 0.15f), 12, 55f, 14f, new Color(1f, 0.95f, 0.7f, 1f), new Color(1f, 0.2f, 0.1f, 0.55f), r);
            }
        }

        // The cube arrives and hangs in the middle, shuddering with the storm.
        float arrive = EaseOut((t - 0.5f) / 0.9f);
        cubePos = Vector2.Lerp(start, centre, arrive) + (t > 1.5f ? new Vector2(Mathf.Sin(t * 31f), Mathf.Cos(t * 27f)) * 3f : Vector2.zero);
        cubeScale = Mathf.Lerp(cubeSize * 0.12f, cubeSize * 0.78f, arrive) * (t > 4.9f && t < 5.0f ? 1.2f : 1f);
        euler = new Vector3(20f + 40f * Mathf.Sin(t * 1.3f), 540f * arrive + t * 70f, Mathf.Sin(t * 2.2f) * 12f);

        // The serpent: flies in, weaves a figure-eight round the cube, then coils into it.
        float dragonStart = 1.4f, coilStart = 3.6f, blastAt = 4.9f;
        if (t >= dragonStart && t < blastAt)
        {
            int n = segs.Length;
            float coil = Mathf.Clamp01((t - coilStart) / (blastAt - coilStart));
            for (int i = 0; i < n; i++)
            {
                float ti = t - i * 0.075f;
                float u = (ti - dragonStart) * 1.5f;
                Vector2 p = SerpentHead(u, size, centre);
                if (coil > 0f)
                {
                    float k = Mathf.Clamp01((ti - coilStart) / (blastAt - coilStart));
                    float a = ti * 9f + i * 0.4f;
                    Vector2 spiral = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (1f - k) * 320f * (1f - i / (float)n * 0.3f);
                    p = Vector2.Lerp(p, spiral, k * k * (3f - 2f * k));
                }
                float along = i / (float)(n - 1);
                float s = Mathf.Lerp(i == 0 ? 120f : 92f, 20f, Mathf.Pow(along, 0.8f)) * (1f - 0.7f * coil * coil);
                Color col = Color.Lerp(new Color(1f, 0.22f, 0.12f, 1f), new Color(0.5f, 0.03f, 0.05f, 1f), along);
                Put(segs[i], p, new Vector2(s, s), 0f, col);
                if (i == 0)
                {
                    Vector2 prev = SerpentHead(u - 0.06f, size, centre);
                    Vector2 dir = (p - prev); dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector2.right;
                    Vector2 perp = new Vector2(-dir.y, dir.x);
                    float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                    Put(feathers[0], p + dir * 34f + perp * 26f, new Vector2(70f, 14f), ang + 150f, new Color(1f, 0.85f, 0.3f, 1f));   // horns
                    Put(feathers[1], p + dir * 34f - perp * 26f, new Vector2(70f, 14f), ang - 150f, new Color(1f, 0.85f, 0.3f, 1f));
                    Put(feathers[2], p + dir * 22f + perp * 24f, new Vector2(22f, 22f), 0f, new Color(1f, 0.95f, 0.3f, 1f));          // eyes
                    Put(feathers[3], p + dir * 22f - perp * 24f, new Vector2(22f, 22f), 0f, new Color(1f, 0.95f, 0.3f, 1f));
                }
            }
            // Crackling arcs off the body, and bolts to the cube now and then.
            for (int a = 0; a < 3; a++)
            {
                int i = Random.Range(0, n);
                float ti = t - i * 0.075f;
                Vector2 p = SerpentHead((ti - dragonStart) * 1.5f, size, centre);
                Bolt(p, p + Random.insideUnitCircle.normalized * Random.Range(90f, 220f), 5, 26f, 5f, new Color(1f, 0.97f, 0.7f, 1f), new Color(1f, 0.3f, 0.1f, 0.5f), null);
            }
            if (Random.value < 0.12f) Bolt(SerpentHead((t - dragonStart) * 1.5f, size, centre), cubePos, 8, 40f, 7f, new Color(1f, 0.97f, 0.7f, 1f), new Color(1f, 0.25f, 0.1f, 0.6f), null);
            if (!fired[6] && t > 1.6f) { fired[6] = true; shake = Mathf.Max(shake, 9f); }
        }
        if (!fired[7] && t >= blastAt)
        {
            fired[7] = true; flash = 1f; shake = 30f;
            SpawnRing(centre, 80f, 1500f, 0.9f, new Color(1f, 0.35f, 0.2f, 0.9f));
            SpawnRing(centre, 60f, 1100f, 0.7f, new Color(1f, 0.9f, 0.5f, 0.9f));
            for (int i = 0; i < 80; i++) SpawnBit(softSprite, centre, Random.insideUnitCircle.normalized * Random.Range(250f, 900f), Random.Range(0.5f, 1.2f), Random.Range(18f, 50f), Color.Lerp(new Color(1f, 0.3f, 0.12f), new Color(1f, 0.95f, 0.6f), Random.value), 0f);
        }
        if (t >= blastAt) for (int i = 0; i < segs.Length; i++) segs[i].enabled = false;   // the serpent has gone into the cube
        if (t >= blastAt) for (int i = 0; i < 4; i++) feathers[i].enabled = false;
        if (t > blastAt && t < blastAt + 0.9f) cubeScale *= 1f + 0.12f * Mathf.Sin((t - blastAt) * 14f) * (1f - (t - blastAt) / 0.9f);
    }

    // ------------------------------------------------------------------
    // RA: the sphere, the sun and the golden wings
    // ------------------------------------------------------------------

    private void DrawRa(float t, float dt, Vector2 size, Vector2 centre, Vector2 start, ref Vector2 cubePos, ref float cubeScale, ref Vector3 euler)
    {
        cubePos = centre; cubeScale = 0f; euler = new Vector3(20f, t * 60f, 0f);
        Color gold = new Color(1f, 0.82f, 0.25f, 1f);

        // 0 - 1.2 s: a golden sphere rises from below and swells, heat rings spreading from it.
        if (t < 1.2f)
        {
            float k = EaseOut(t / 1.2f);
            Vector2 p = new Vector2(centre.x, Mathf.Lerp(-120f, centre.y, k));
            float s = Mathf.Lerp(60f, 380f, k) * (1f + 0.06f * Mathf.Sin(t * 22f));
            Put(sphere, p, new Vector2(s, s), 0f, new Color(1f, 0.85f, 0.35f, 1f));
            if (Random.value < dt * 5f) SpawnRing(p, s * 0.5f, s * 2.2f, 0.9f, new Color(1f, 0.7f, 0.2f, 0.8f));
        }
        else sphere.enabled = false;

        // 1.2 s: the sphere bursts into the sun.
        if (!fired[0] && t >= 1.2f)
        {
            fired[0] = true; flash = 1f; shake = 14f;
            SpawnRing(centre, 200f, 1800f, 1.0f, new Color(1f, 0.95f, 0.7f, 0.9f));
            for (int i = 0; i < 70; i++) SpawnBit(softSprite, centre, Random.insideUnitCircle.normalized * Random.Range(200f, 800f), Random.Range(0.6f, 1.4f), Random.Range(20f, 56f), Color.Lerp(gold, Color.white, Random.value * 0.6f), 0f);
        }
        float sunK = Smooth((t - 1.2f) / 1.4f);
        float sunFade = 1f - Smooth((t - 4.2f) / 0.8f);
        if (t >= 1.2f)
        {
            float ss = Mathf.Lerp(380f, 1800f, EaseOut((t - 1.2f) / 1.6f)) * (1f + 0.03f * Mathf.Sin(t * 6f));
            Put(sun, centre, new Vector2(ss, ss), t * 14f, new Color(1f, 0.9f, 0.55f, Mathf.Lerp(0.95f, 0.5f, sunK) * sunFade));
            Put(glowBehind, centre, new Vector2(ss * 0.5f, ss * 0.5f), 0f, new Color(1f, 0.7f, 0.2f, 0.6f * sunFade));
        }
        else { sun.enabled = false; glowBehind.enabled = false; }

        // The cube appears in the sun's heart.
        float cubeK = EaseOut((t - 1.3f) / 0.7f);
        cubeScale = cubeSize * 0.8f * cubeK * (1f + 0.05f * Mathf.Sin(t * 5f));
        cubePos = centre + new Vector2(0f, Mathf.Sin(t * 3f) * 10f);

        // 1.4 - 4.0 s: the wings beat (in time with the sound); at 4.0 they fold in with a golden shockwave.
        float wingIn = Smooth((t - 1.4f) / 0.5f);
        float fold = Smooth((t - 4.0f) / 0.7f);
        if (!fired[1] && t >= 4.0f)
        {
            fired[1] = true; flash = 0.9f; shake = 16f;
            SpawnRing(centre, 100f, 1400f, 0.9f, new Color(1f, 0.85f, 0.4f, 0.9f));
            SpawnRing(centre, 80f, 1000f, 0.7f, new Color(1f, 1f, 0.8f, 0.9f));
            for (int i = 0; i < 60; i++) SpawnBit(softSprite, centre, Random.insideUnitCircle.normalized * Random.Range(250f, 800f), Random.Range(0.6f, 1.3f), Random.Range(18f, 46f), Color.Lerp(gold, Color.white, Random.value * 0.7f), 0f);
        }
        float wingAlpha = wingIn * (1f - fold);
        float beat = Mathf.Sin((t - 1.4f) * (Mathf.PI * 2f / 0.45f));
        if (wingAlpha > 0.01f)
        {
            int perWing = 10;
            for (int w = 0; w < 2; w++)
            {
                float side = w == 0 ? 1f : -1f;
                for (int j = 0; j < perWing; j++)
                {
                    float flap = 24f * beat * (1f - fold);
                    float rot = -side * (18f + j * 10f) * (1f - fold * 0.8f) + side * flap;
                    float len = 330f + 34f * j * (1f - j / 13f);
                    Color col = Color.Lerp(new Color(1f, 0.9f, 0.4f, 1f), new Color(1f, 0.45f, 0.1f, 1f), j / (float)(perWing - 1)); col.a = 0.92f * wingAlpha;
                    Image f = feathers[4 + w * perWing + j];
                    Vector2 shoulder = centre + new Vector2(side * 110f, -10f + j * 2f);
                    f.enabled = true;
                    RectTransform r = f.rectTransform;
                    r.pivot = new Vector2(0.5f, 0f);
                    r.anchoredPosition = shoulder;
                    r.sizeDelta = new Vector2(78f, len * wingIn);
                    r.localRotation = Quaternion.Euler(0f, 0f, rot);
                    f.color = col;
                }
            }
            // Golden feathers and sparks drift down from the wings.
            if (Random.value < dt * 45f * wingAlpha) SpawnBit(softSprite, centre + new Vector2(Random.Range(-1f, 1f) * 520f, Random.Range(-40f, 220f)), new Vector2(Random.Range(-40f, 40f), Random.Range(-60f, -140f)), Random.Range(1f, 1.8f), Random.Range(14f, 34f), Color.Lerp(gold, Color.white, Random.value * 0.5f), 20f);
        }
        else for (int i = 4; i < feathers.Length; i++) feathers[i].enabled = false;
    }

    // ------------------------------------------------------------------
    // OBELISK: the rumble, the fist and the slam
    // ------------------------------------------------------------------

    private void DrawFist(Vector2 pos, float alpha)
    {
        // fist[]: 0 forearm outline, 1 forearm, 2 palm outline, 3 palm, 4-7 finger outlines, 8-11 fingers, 12 thumb outline, 13 thumb, 14-17 knuckle highlights
        Color outline = new Color(0.02f, 0.07f, 0.3f, alpha), fill = new Color(0.16f, 0.32f, 0.86f, alpha), light = new Color(0.55f, 0.78f, 1f, alpha * 0.8f);
        Put(fist[0], pos + new Vector2(0f, 140f + 700f), new Vector2(330f, 1400f), 0f, outline);
        Put(fist[1], pos + new Vector2(0f, 140f + 700f), new Vector2(300f, 1400f), 0f, fill);
        Put(fist[2], pos, new Vector2(400f, 300f), 0f, outline);
        Put(fist[3], pos, new Vector2(372f, 272f), 0f, fill);
        for (int i = 0; i < 4; i++)
        {
            Vector2 fp = pos + new Vector2(-132f + i * 88f, -190f);
            Put(fist[4 + i], fp, new Vector2(98f, 150f), 0f, outline);
            Put(fist[8 + i], fp, new Vector2(80f, 132f), 0f, fill);
            Put(fist[14 + i], fp + new Vector2(-16f, -34f), new Vector2(14f, 56f), 0f, light);
        }
        Put(fist[12], pos + new Vector2(-222f, -30f), new Vector2(92f, 200f), 18f, outline);
        Put(fist[13], pos + new Vector2(-222f, -30f), new Vector2(74f, 182f), 18f, fill);
    }

    private void DrawObelisk(float t, float dt, Vector2 size, Vector2 centre, Vector2 start, ref Vector2 cubePos, ref float cubeScale, ref Vector3 euler)
    {
        float impact = 2.55f;
        cubePos = centre; cubeScale = 0f; euler = new Vector3(20f, t * 50f, 0f);

        // Rumble: the screen shakes harder and harder while rubble falls.
        if (t < impact) shake = Mathf.Max(shake, Mathf.Lerp(1f, 14f, Mathf.Pow(t / impact, 2f)));
        float rubbleRate = Mathf.Lerp(18f, 70f, Mathf.Clamp01(t / impact)) * (t < 3.6f ? 1f : 0f);
        if (Random.value < dt * rubbleRate)
            SpawnBit(squareSprite, new Vector2(Random.Range(0f, size.x), size.y + 30f), new Vector2(Random.Range(-60f, 60f), -Random.Range(320f, 760f)), Random.Range(1.2f, 2f), Random.Range(10f, 34f), Color.Lerp(new Color(0.25f, 0.3f, 0.5f), new Color(0.6f, 0.7f, 0.9f), Random.value), -200f);
        for (int i = 0; i < clouds.Length; i++)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 1.6f + i * 1.3f);
            float a = (0.25f + 0.2f * pulse) * Smooth(t / 1f) * (1f - Smooth((t - 4.4f) / 1f));
            Put(clouds[i], new Vector2(size.x * (0.1f + 0.2f * i), size.y * (0.25f + 0.5f * ((i * 0.41f) % 1f))), new Vector2(760f, 560f), 0f, new Color(0.18f, 0.4f, 1f, a));
        }

        // The fist: lowers into view, cocks back and shudders, then SLAMS.
        float fistY;
        float fistAlpha = 1f;
        float lower = Mathf.Clamp01((t - 1.3f) / 1.0f);
        if (t < 2.3f) fistY = Mathf.Lerp(size.y + 520f, size.y * 0.80f, EaseOut(lower)) + (t > 1.9f ? Mathf.Sin(t * 60f) * 5f : 0f);
        else if (t < impact) { float k = (t - 2.3f) / (impact - 2.3f); fistY = Mathf.Lerp(size.y * 0.80f, centre.y + 40f, k * k * k); }
        else
        {
            float d = t - impact;
            fistY = centre.y + 40f + (d > 0.4f ? Mathf.Pow((d - 0.4f) / 0.9f, 2f) * 700f : 0f);   // it holds for a moment, then lifts away
            fistAlpha = 1f - Smooth((d - 0.5f) / 0.7f);
        }
        if (t >= 1.3f && fistAlpha > 0.01f)
        {
            Put(glowBehind, new Vector2(centre.x, fistY - 80f), new Vector2(900f, 900f), 0f, new Color(0.3f, 0.55f, 1f, 0.45f * fistAlpha));
            DrawFist(new Vector2(centre.x, fistY), fistAlpha);
        }
        else { glowBehind.enabled = false; for (int i = 0; i < fist.Length; i++) fist[i].enabled = false; }

        // The impact.
        if (!fired[0] && t >= impact)
        {
            fired[0] = true; flash = 1f; shake = 38f; impactTime = t; impactPoint = new Vector2(centre.x, centre.y - 20f);
            SpawnRing(impactPoint, 60f, 1700f, 0.9f, new Color(0.7f, 0.9f, 1f, 0.95f));
            SpawnRing(impactPoint, 40f, 1250f, 0.75f, new Color(0.3f, 0.55f, 1f, 0.9f));
            SpawnRing(impactPoint, 30f, 800f, 0.6f, new Color(1f, 1f, 1f, 0.9f));
            for (int i = 0; i < 90; i++) SpawnBit(squareSprite, impactPoint, Random.insideUnitCircle.normalized * Random.Range(250f, 1100f) + Vector2.up * 300f, Random.Range(0.9f, 1.8f), Random.Range(12f, 44f), Color.Lerp(new Color(0.2f, 0.3f, 0.6f), new Color(0.75f, 0.85f, 1f), Random.value), -1400f);
            System.Random r = new System.Random(Random.Range(0, 99999));
            for (int c = 0; c < 14; c++)
            {
                float ang = (c / 14f + (float)r.NextDouble() * 0.05f) * Mathf.PI * 2f, len = 280f + (float)r.NextDouble() * 650f;
                Vector2[] pts = new Vector2[6];
                for (int p = 0; p < pts.Length; p++)
                {
                    float k = p / (float)(pts.Length - 1);
                    float jitter = ((float)r.NextDouble() - 0.5f) * 0.22f;
                    pts[p] = impactPoint + new Vector2(Mathf.Cos(ang + jitter), Mathf.Sin(ang + jitter)) * len * k;
                }
                cracks.Add(pts);
            }
        }
        if (impactTime > 0f)
        {
            float d = t - impactTime;
            float ca = (1f - Smooth((d - 1.4f) / 1.6f));
            foreach (Vector2[] pts in cracks)
                for (int p = 0; p < pts.Length - 1; p++)
                {
                    Line(pts[p], pts[p + 1], Mathf.Lerp(14f, 3f, p / 5f), new Color(0.55f, 0.85f, 1f, 0.35f * ca));
                    Line(pts[p], pts[p + 1], Mathf.Lerp(5f, 1.5f, p / 5f), new Color(0.95f, 1f, 1f, ca));
                }
            // The cube rises out of the crater, spinning.
            float up = EaseOut((d - 0.15f) / 0.9f);
            cubeScale = cubeSize * 0.85f * up * (1f + 0.08f * Mathf.Sin(d * 5f));
            cubePos = Vector2.Lerp(impactPoint, centre + new Vector2(0f, size.y * 0.04f), up) + new Vector2(0f, Mathf.Sin(d * 2.6f) * 12f);
            euler = new Vector3(20f + 25f * Mathf.Sin(d * 1.7f), d * 140f, 0f);
            if (d > 0.3f && Random.value < dt * 30f) SpawnBit(squareSprite, cubePos + Random.insideUnitCircle * cubeScale * 0.5f, new Vector2(Random.Range(-30f, 30f), Random.Range(30f, 110f)), Random.Range(0.8f, 1.5f), Random.Range(8f, 20f), new Color(0.6f, 0.85f, 1f), 0f);
        }
    }

    // ------------------------------------------------------------------
    // Drawing helpers
    // ------------------------------------------------------------------

    private static void Put(Image img, Vector2 pos, Vector2 size, float rot, Color c)
    {
        if (img == null) return;
        if (!img.enabled) img.enabled = true;
        RectTransform r = img.rectTransform;
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos;
        r.sizeDelta = size;
        r.localRotation = Quaternion.Euler(0f, 0f, rot);
        img.color = c;
    }

    private void Line(Vector2 a, Vector2 b, float width, Color c)
    {
        if (lines == null || lineCount >= lines.Length) return;
        Image img = lines[lineCount++];
        Vector2 d = b - a;
        Put(img, (a + b) * 0.5f, new Vector2(d.magnitude + width * 0.5f, width), Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, c);
    }

    /// <summary>A jagged lightning bolt (a wide faint glow line and a thin bright core per piece).</summary>
    private void Bolt(Vector2 a, Vector2 b, int parts, float jitter, float width, Color core, Color glow, System.Random rng)
    {
        Vector2 prev = a;
        Vector2 dir = b - a; Vector2 perp = dir.sqrMagnitude > 0.01f ? new Vector2(-dir.y, dir.x).normalized : Vector2.up;
        for (int i = 1; i <= parts; i++)
        {
            float k = i / (float)parts;
            float rnd = rng != null ? (float)rng.NextDouble() : Random.value;
            Vector2 p = Vector2.Lerp(a, b, k) + (i == parts ? Vector2.zero : perp * (rnd - 0.5f) * 2f * jitter);
            Line(prev, p, width * 2.6f, glow);
            Line(prev, p, width * 0.8f, core);
            prev = p;
        }
    }

    private void SpawnBit(Sprite sprite, Vector2 pos, Vector2 vel, float life, float size, Color color, float gravity)
    {
        if (bits == null) return;
        for (int i = 0; i < bits.Length; i++)
        {
            if (bits[i].alive) continue;
            bits[i].alive = true; bits[i].pos = pos; bits[i].vel = vel; bits[i].life = bits[i].maxLife = life; bits[i].size = size;
            bits[i].spin = Random.Range(-240f, 240f); bits[i].gravity = gravity;
            bits[i].image.sprite = sprite; bits[i].image.color = color;
            bits[i].rect.gameObject.SetActive(true);
            return;
        }
    }

    private void UpdateBits(float dt)
    {
        if (bits == null) return;
        for (int i = 0; i < bits.Length; i++)
        {
            if (!bits[i].alive) continue;
            bits[i].life -= dt;
            if (bits[i].life <= 0f) { bits[i].alive = false; bits[i].rect.gameObject.SetActive(false); continue; }
            bits[i].vel += new Vector2(0f, bits[i].gravity) * dt;
            bits[i].pos += bits[i].vel * dt;
            float k = bits[i].life / bits[i].maxLife;
            bits[i].rect.anchoredPosition = bits[i].pos;
            float s = bits[i].size * Mathf.Clamp01(k * 2f);
            bits[i].rect.sizeDelta = new Vector2(s, s);
            bits[i].rect.localRotation = Quaternion.Euler(0f, 0f, (1f - k) * bits[i].spin);
            Color c = bits[i].image.color; c.a = Mathf.Clamp01(k * 1.8f); bits[i].image.color = c;
        }
    }

    private void SpawnRing(Vector2 pos, float from, float to, float life, Color color)
    {
        if (rings == null) return;
        for (int i = 0; i < rings.Length; i++)
        {
            if (rings[i].alive) continue;
            rings[i].alive = true; rings[i].pos = pos; rings[i].age = 0f; rings[i].life = life; rings[i].from = from; rings[i].to = to; rings[i].color = color;
            rings[i].rect.gameObject.SetActive(true);
            return;
        }
    }

    private void UpdateRings(float dt)
    {
        if (rings == null) return;
        for (int i = 0; i < rings.Length; i++)
        {
            if (!rings[i].alive) continue;
            rings[i].age += dt;
            float k = rings[i].age / rings[i].life;
            if (k >= 1f) { rings[i].alive = false; rings[i].rect.gameObject.SetActive(false); continue; }
            float s = Mathf.Lerp(rings[i].from, rings[i].to, EaseOut(k));
            rings[i].rect.anchoredPosition = rings[i].pos;
            rings[i].rect.sizeDelta = new Vector2(s, s);
            Color c = rings[i].color; c.a *= 1f - k; rings[i].image.color = c;
        }
    }

    // ------------------------------------------------------------------
    // INFINITY STONES: one shared timeline (cube arrives, a signature effect builds, a climax at 3.2 s), a different effect each
    // ------------------------------------------------------------------

    private void DrawStone(int stone, float t, float dt, Vector2 size, Vector2 centre, Vector2 start, ref Vector2 cubePos, ref float cubeScale, ref Vector3 euler)
    {
        Color c = PixelInfinity.StoneColor(stone);
        Color bright = Color.Lerp(c, Color.white, 0.6f);
        float arrive = EaseOut((t - 0.3f) / 1.4f);
        cubePos = Vector2.Lerp(start, centre, arrive);
        float pop = t > 3.2f && t < 3.4f ? 1.18f : 1f;
        cubeScale = Mathf.Lerp(cubeSize * 0.12f, cubeSize * 0.7f, arrive) * pop;
        euler = new Vector3(20f + 12f * Mathf.Sin(t * 1.7f), t * 80f * (0.6f + arrive), 0f);

        // The glow behind the cube swells with the build-up.
        float build = Mathf.Clamp01(t / 3.2f);
        float g = Mathf.Lerp(200f, 900f, build * build) * (1f + 0.05f * Mathf.Sin(t * 14f));
        Put(glowBehind, centre, new Vector2(g, g), 0f, new Color(c.r, c.g, c.b, 0.5f * Smooth(t / 0.6f) * (1f - Smooth((t - 4.2f) / 0.8f))));

        switch (stone)
        {
            case 0: // SPACE: a tesseract of two turning squares joined at the corners, a swirl of stars pulled in, rings that shrink to the cube.
            {
                float r = Mathf.Lerp(140f, 330f, arrive) * (1f - 0.35f * Smooth((t - 3.0f) / 0.4f));
                for (int sq = 0; sq < 2; sq++)
                {
                    float rad = r * (sq == 0 ? 1f : 0.62f), rot = t * (sq == 0 ? 55f : -85f);
                    for (int i = 0; i < 4; i++)
                    {
                        float a0 = (rot + i * 90f) * Mathf.Deg2Rad, a1 = (rot + (i + 1) * 90f) * Mathf.Deg2Rad;
                        Line(centre + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * rad * 1.41f, centre + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * rad * 1.41f, 6f, new Color(bright.r, bright.g, bright.b, 0.9f * arrive));
                    }
                }
                for (int i = 0; i < 4; i++)
                {
                    float a = (t * 55f + i * 90f) * Mathf.Deg2Rad, b = (-t * 85f + i * 90f) * Mathf.Deg2Rad;
                    Line(centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r * 1.41f, centre + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * r * 0.62f * 1.41f, 4f, new Color(c.r, c.g, c.b, 0.7f * arrive));
                }
                if (t < 3.2f && Random.value < dt * 70f)
                {
                    float a = Random.value * 6.283f; Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    SpawnBit(softSprite, centre + dir * 620f, -dir * 380f + new Vector2(-dir.y, dir.x) * 170f, 1.5f, Random.Range(10f, 26f), Color.Lerp(c, Color.white, Random.value * 0.7f), 0f);
                }
                if (t < 3.0f && Random.value < dt * 2.5f) SpawnRing(centre, 1000f, 240f, 1.2f, new Color(c.r, c.g, c.b, 0.8f));
                break;
            }
            case 1: // MIND: thought-bolts crackle in from the screen's edges, rings pulse out in time, a bright scalar sparkle.
            {
                if (t > 0.6f && t < 3.2f && Random.value < dt * 9f)
                {
                    System.Random r = new System.Random(Random.Range(0, 99999));
                    float a = Random.value * 6.283f;
                    Bolt(centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * size.x * 0.55f, centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 160f, 9, 40f, 7f, new Color(1f, 1f, 0.85f, 1f), new Color(c.r, c.g, c.b, 0.5f), r);
                }
                float beat = Mathf.Repeat(t * 1.8f, 1f);
                if (beat < dt * 1.8f && t > 0.4f && t < 3.4f) SpawnRing(centre, 200f, 900f, 1.0f, new Color(c.r, c.g, c.b, 0.7f));
                if (t > 0.8f && Random.value < dt * 30f) SpawnBit(softSprite, centre + Random.insideUnitCircle * 220f, Random.insideUnitCircle * 60f, 0.9f, 22f, bright, 0f);
                break;
            }
            case 2: // REALITY: shapes tear loose and tumble, the screen warps (shaking harder), red and white flickers, a wobbling ring of squares.
            {
                shake = Mathf.Max(shake, 2f + 8f * build);
                if (Random.value < dt * 45f && t < 3.4f)
                {
                    Vector2 p = new Vector2(Random.Range(0.1f, 0.9f) * size.x, Random.Range(0.1f, 0.9f) * size.y);
                    SpawnBit(squareSprite, p, (centre - p) * 0.25f + Random.insideUnitCircle * 80f, Random.Range(0.8f, 1.6f), Random.Range(14f, 60f), Random.value < 0.3f ? Color.white : c, 0f);
                }
                if (Random.value < dt * 3f && t < 3.2f) flash = Mathf.Max(flash, 0.35f);
                for (int i = 0; i < 12; i++)
                {
                    float a = i / 12f * 6.283f + t * 1.4f; float rad = 300f + 40f * Mathf.Sin(t * 5f + i * 1.7f);
                    Put(feathers[i], centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rad, new Vector2(40f, 40f), a * Mathf.Rad2Deg + t * 90f, new Color(c.r, c.g, c.b, 0.85f * arrive));
                    feathers[i].sprite = squareSprite;
                }
                break;
            }
            case 3: // POWER: energy converges along rays, the shaking grows, then everything detonates.
            {
                shake = Mathf.Max(shake, 2f + 16f * build * build);
                if (t < 3.2f)
                {
                    int rays = 14;
                    for (int i = 0; i < rays; i++)
                    {
                        float a = i / (float)rays * 6.283f + t * 0.5f;
                        Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        float head = Mathf.Repeat(t * 1.5f + i * 0.37f, 1f);
                        Line(centre + dir * Mathf.Lerp(800f, 150f, head), centre + dir * Mathf.Lerp(800f, 150f, Mathf.Min(1f, head + 0.12f)), 5f + 8f * build, new Color(bright.r, bright.g, bright.b, 0.4f + 0.5f * build));
                    }
                }
                break;
            }
            case 4: // TIME: a clock face whose hands run backwards, then forwards, with ticks, and sand flowing the wrong way.
            {
                float rad = Mathf.Lerp(200f, 380f, arrive);
                for (int i = 0; i < 24; i++)
                {
                    float a = i / 24f * 6.283f;
                    Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    float len = i % 6 == 0 ? 36f : 18f;
                    Line(centre + d * rad, centre + d * (rad - len), i % 6 == 0 ? 8f : 4f, new Color(bright.r, bright.g, bright.b, 0.85f * arrive));
                }
                float rev = t < 2.6f ? -1f : 1f;
                float minuteDeg = t < 2.6f ? -t * 130f : -2.6f * 130f + (t - 2.6f) * 520f;   // backwards first, then a fast forward
                float am = Mathf.Deg2Rad * (90f + minuteDeg);
                float ah = Mathf.Deg2Rad * (90f + minuteDeg / 12f);
                Line(centre, centre + new Vector2(Mathf.Cos(am), Mathf.Sin(am)) * rad * 0.85f, 8f, new Color(1f, 1f, 1f, 0.95f * arrive));
                Line(centre, centre + new Vector2(Mathf.Cos(ah), Mathf.Sin(ah)) * rad * 0.55f, 12f, new Color(bright.r, bright.g, bright.b, 0.95f * arrive));
                if (Random.value < dt * 50f && t < 3.4f)
                {
                    float a = Random.value * 6.283f; Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    SpawnBit(softSprite, centre + d * Random.Range(rad * 0.4f, rad * 1.3f), -d * 90f * rev * -1f, 1.1f, Random.Range(8f, 18f), bright, 0f);
                }
                if (Random.value < dt * 2f && t < 2.6f) SpawnRing(centre, 120f, 700f, 1.2f, new Color(c.r, c.g, c.b, 0.6f));
                break;
            }
            default: // SOUL: orange wisps rise from the bottom of the screen, two ribbons of light weave across, a halo of spirits circles the cube.
            {
                if (Random.value < dt * 55f && t < 3.6f)
                    SpawnBit(softSprite, new Vector2(Random.value * size.x, -20f), new Vector2(Random.Range(-30f, 30f), Random.Range(240f, 460f)), Random.Range(1.4f, 2.4f), Random.Range(18f, 44f), new Color(c.r, c.g, c.b, 0.8f), 0f);
                for (int rb = 0; rb < 2; rb++)
                {
                    Vector2 prev = Vector2.zero;
                    for (int i = 0; i <= 22; i++)
                    {
                        float u = i / 22f;
                        Vector2 p = new Vector2(u * size.x, centre.y + Mathf.Sin(u * 7f + t * 2.2f + rb * 3.1f) * 90f * Smooth(t / 1.0f) + (rb == 0 ? 60f : -60f));
                        if (i > 0) Line(prev, p, rb == 0 ? 10f : 6f, new Color(bright.r, bright.g, bright.b, 0.55f * Smooth(t / 0.8f) * (1f - Smooth((t - 3.8f) / 0.8f))));
                        prev = p;
                    }
                }
                for (int i = 0; i < 6; i++)
                {
                    float a = t * 1.6f + i / 6f * 6.283f;
                    Put(segs[i], centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.45f) * 330f, new Vector2(46f, 46f), 0f, new Color(1f, 0.9f, 0.7f, 0.8f * arrive));
                }
                break;
            }
        }

        // The climax, the same shape for every stone: a flash, a burst of bits and two shock rings.
        if (!fired[0] && t >= 3.2f)
        {
            fired[0] = true; flash = 1f; shake = Mathf.Max(shake, stone == 3 ? 26f : 12f);
            for (int i = 0; i < (stone == 3 ? 110 : 60); i++) SpawnBit(softSprite, centre, Random.insideUnitCircle.normalized * Random.Range(160f, stone == 3 ? 900f : 560f), Random.Range(0.7f, 1.5f), Random.Range(14f, 40f), Color.Lerp(c, Color.white, Random.value * 0.6f), 0f);
            SpawnRing(centre, 200f, 1500f, 0.9f, new Color(1f, 1f, 1f, 0.8f));
            SpawnRing(centre, 150f, 1000f, 1.2f, new Color(c.r, c.g, c.b, 0.9f));
        }
    }

    // ------------------------------------------------------------------
    // INFINITY CUBELETTE: six streams of colour converge, a gold hexagon of light closes round the cube, the snap
    // ------------------------------------------------------------------

    private void DrawCubelette(float t, float dt, Vector2 size, Vector2 centre, Vector2 start, ref Vector2 cubePos, ref float cubeScale, ref Vector3 euler)
    {
        float form = Smooth((t - 3.4f) / 0.6f);
        cubePos = centre;
        cubeScale = cubeSize * 0.85f * form * (t > 4.0f && t < 4.2f ? 1.15f : 1f);
        euler = new Vector3(18f, t * 100f, 0f);
        float build = Mathf.Clamp01(t / 3.4f);

        // Six streams, one per stone colour, swirl in from six directions.
        if (t < 3.4f)
        {
            for (int s = 0; s < 6; s++)
            {
                Color c = PixelInfinity.StoneColor(s);
                float baseA = s / 6f * 6.283f + 0.5f;
                if (Random.value < dt * 28f)
                {
                    float a = baseA + Random.Range(-0.15f, 0.15f); Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    SpawnBit(softSprite, centre + dir * 760f, -dir * 520f + new Vector2(-dir.y, dir.x) * 260f, 1.4f, Random.Range(14f, 34f), c, 0f);
                }
                float ang = baseA + t * 0.9f;
                float rad = Mathf.Lerp(480f, 120f, build * build);
                Put(segs[s], centre + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rad, new Vector2(70f, 70f), 0f, new Color(c.r, c.g, c.b, 0.9f));
            }
            float g = Mathf.Lerp(80f, 700f, build * build);
            Put(glowBehind, centre, new Vector2(g, g), 0f, new Color(1f, 0.95f, 0.7f, 0.6f * build));
        }

        // The hexagon of light between the six, once they meet.
        if (t > 2.4f && t < 4.8f)
        {
            float k = Smooth((t - 2.4f) / 0.8f) * (1f - Smooth((t - 4.2f) / 0.6f));
            float rad = 330f * (1f - 0.2f * Smooth((t - 3.0f) / 0.6f));
            for (int i = 0; i < 6; i++)
            {
                float a0 = (i / 6f * 360f + t * 20f) * Mathf.Deg2Rad, a1 = ((i + 1) / 6f * 360f + t * 20f) * Mathf.Deg2Rad;
                Color c = PixelInfinity.StoneColor(i);
                Line(centre + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * rad, centre + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * rad, 9f, new Color(c.r, c.g, c.b, k));
            }
        }

        if (!fired[0] && t >= 3.4f)
        {
            fired[0] = true; flash = 1f; shake = 18f;
            for (int i = 0; i < 6; i++) SpawnRing(centre, 150f, 1100f + i * 120f, 1.1f + i * 0.1f, PixelInfinity.StoneColor(i));
            for (int i = 0; i < 90; i++) SpawnBit(softSprite, centre, Random.insideUnitCircle.normalized * Random.Range(180f, 800f), Random.Range(0.8f, 1.6f), Random.Range(14f, 40f), PixelInfinity.StoneColor(i % 6), 0f);
        }
        if (!fired[1] && t >= 5.0f) { fired[1] = true; flash = 0.8f; for (int i = 0; i < 40; i++) SpawnBit(softSprite, centre + Random.insideUnitCircle * 150f, Random.insideUnitCircle * 160f + Vector2.up * 80f, Random.Range(1f, 1.8f), Random.Range(8f, 20f), new Color(1f, 0.95f, 0.7f, 1f), -40f); }
    }

    // ------------------------------------------------------------------
    // Sprites
    // ------------------------------------------------------------------

    private static Sprite MakeSprite(int w, int h, System.Func<float, float, float> alphaAt)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(alphaAt((x + 0.5f) / w * 2f - 1f, (y + 0.5f) / h))));
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
    }

    private static void EnsureSprites()
    {
        if (softSprite == null) softSprite = MakeSprite(64, 64, (u, v) => { float vy = v * 2f - 1f; float r = Mathf.Sqrt(u * u + vy * vy); return Mathf.Pow(Mathf.Clamp01(1f - r), 1.6f); });
        if (squareSprite == null) squareSprite = MakeSprite(8, 8, (u, v) => 1f);
        if (ringSprite == null) ringSprite = MakeSprite(128, 128, (u, v) => { float vy = v * 2f - 1f; float r = Mathf.Sqrt(u * u + vy * vy); return Mathf.Exp(-Mathf.Pow((r - 0.9f) / 0.07f, 2f)) * (r < 1f ? 1f : 0f); });
        if (sunSprite == null) sunSprite = MakeSprite(256, 256, (u, v) =>
        {
            float vy = v * 2f - 1f; float r = Mathf.Sqrt(u * u + vy * vy);
            float falloff = Mathf.Clamp01(1f - r);
            float rays = Mathf.Pow(Mathf.Max(0f, Mathf.Cos(Mathf.Atan2(vy, u) * 14f)), 10f) * Mathf.Pow(falloff, 1.1f);
            return Mathf.Pow(falloff, 2.4f) * 0.75f + rays * 0.95f;
        });
        if (featherSprite == null) featherSprite = MakeSprite(32, 96, (u, v) =>
        {
            float width = Mathf.Lerp(1f, 0.08f, Mathf.Pow(v, 0.9f));   // wide at the base, a point at the tip
            float edge = Mathf.Clamp01((width - Mathf.Abs(u)) * 5f);
            return edge * Mathf.Lerp(1f, 0.35f, v);
        });
    }

    // ------------------------------------------------------------------
    // Building and cleaning up
    // ------------------------------------------------------------------

    private static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.sprite = sprite; img.color = color; img.raycastTarget = false;
        RectTransform r = img.rectTransform;
        r.anchorMin = r.anchorMax = Vector2.zero;
        r.pivot = new Vector2(0.5f, 0.5f);
        return img;
    }

    private Image[] Pool(string name, Transform parent, Sprite sprite, int count)
    {
        Image[] arr = new Image[count];
        for (int i = 0; i < count; i++) { arr[i] = NewImage(name, parent, sprite, Color.white); arr[i].enabled = false; }
        return arr;
    }

    private void Build(int tierIndex)
    {
        Cleanup();
        EnsureSprites();
        TMP_FontAsset font = clicker.UIFont;
        canvasRoot = PixelUIKit.CreateCanvas("God Find", 691, new Vector2(1920f, 1080f), true);
        canvasRect = canvasRoot.GetComponent<RectTransform>();

        dim = NewImage("Dark", canvasRoot.transform, null, new Color(0f, 0f, 0f, 0f));
        PixelUIKit.Stretch(dim.rectTransform);
        dim.raycastTarget = true;   // blocks clicks on the cube and the UI while it plays

        GameObject stageGo = new GameObject("Stage", typeof(RectTransform), typeof(CanvasGroup));
        stageGo.transform.SetParent(canvasRoot.transform, false);
        stage = stageGo.GetComponent<RectTransform>();
        PixelUIKit.Stretch(stage);

        clouds = Pool("Cloud", stage, softSprite, 8);
        sun = NewImage("Sun", stage, sunSprite, Color.white); sun.enabled = false;
        glowBehind = NewImage("Glow", stage, softSprite, Color.white); glowBehind.enabled = false;
        sphere = NewImage("Sphere", stage, softSprite, Color.white); sphere.enabled = false;
        rings = new Ring[10];
        for (int i = 0; i < rings.Length; i++)
        {
            Image img = NewImage("Ring", stage, ringSprite, Color.white);
            rings[i].image = img; rings[i].rect = img.rectTransform; img.gameObject.SetActive(false);
        }
        feathers = Pool("Feather", stage, featherSprite, 24);
        // The serpent's horns and eyes (feathers[0..3]) use plain shapes, not feather pictures.
        for (int i = 0; i < 4; i++) feathers[i].sprite = i < 2 ? squareSprite : softSprite;
        segs = Pool("Segment", stage, softSprite, 30);
        fist = Pool("Fist", stage, squareSprite, 20);
        lines = Pool("Line", stage, squareSprite, 230);
        bits = new Bit[260];
        for (int i = 0; i < bits.Length; i++)
        {
            Image img = NewImage("Bit", stage, softSprite, Color.white);
            bits[i].image = img; bits[i].rect = img.rectTransform; img.gameObject.SetActive(false);
        }

        GameObject cubeGo = new GameObject("Cube", typeof(RectTransform), typeof(RawImage));
        cubeGo.transform.SetParent(canvasRoot.transform, false);
        cubeImage = cubeGo.GetComponent<RawImage>();
        cubeImage.raycastTarget = false;
        cubeRect = cubeGo.GetComponent<RectTransform>();
        cubeRect.anchorMin = cubeRect.anchorMax = Vector2.zero;
        cubeRect.pivot = new Vector2(0.5f, 0.5f);

        backpack = NewImage("Backpack", canvasRoot.transform, PixelGhostMinigame.BuildBackpackSprite(), new Color(1f, 1f, 1f, 0f));
        backpack.rectTransform.sizeDelta = new Vector2(backpackSize, backpackSize);
        flashImage = NewImage("Flash", canvasRoot.transform, null, new Color(1f, 1f, 1f, 0f));
        PixelUIKit.Stretch(flashImage.rectTransform);

        float bar = PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f;
        title = PixelUIKit.CreateText(font, canvasRoot.transform, "Title", "", 68f, TextAlignmentOptions.Center, FontStyles.Bold, Color.white);
        title.raycastTarget = false;
        RectTransform tr = title.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f); tr.pivot = new Vector2(0.5f, 1f);
        tr.sizeDelta = new Vector2(0f, 100f); tr.anchoredPosition = new Vector2(0f, -(bar + 60f));
        subtitle = PixelUIKit.CreateText(font, canvasRoot.transform, "Subtitle", "", 38f, TextAlignmentOptions.Center, FontStyles.Bold, new Color(1f, 1f, 1f, 0.9f));
        subtitle.raycastTarget = false;
        RectTransform sr = subtitle.rectTransform;
        sr.anchorMin = new Vector2(0f, 1f); sr.anchorMax = new Vector2(1f, 1f); sr.pivot = new Vector2(0.5f, 1f);
        sr.sizeDelta = new Vector2(0f, 60f); sr.anchoredPosition = new Vector2(0f, -(bar + 160f));
        title.alpha = 0f; subtitle.alpha = 0f;

        BuildStudio(tierIndex);
        cubeImage.texture = rt;
        cubeImage.enabled = false;
    }

    private void BuildStudio(int tierIndex)
    {
        studioRoot = new GameObject("God Find Studio");
        studioRoot.transform.position = new Vector3(0f, -4700f, 0f);
        pivot = new GameObject("Pivot").transform;
        pivot.SetParent(studioRoot.transform, false);
        GameObject model = clicker.CreateDisplayPixel(tierIndex, pivot, 1f);
        if (model == null)
        {
            model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(model.GetComponent<Collider>());
            model.transform.SetParent(pivot, false);
        }
        foreach (Collider col in model.GetComponentsInChildren<Collider>()) Destroy(col);
        model.transform.localPosition = Vector3.zero;

        rt = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32) { name = "God Find Picture" };
        GameObject camGo = new GameObject("Camera");
        camGo.transform.SetParent(studioRoot.transform, false);
        studioCam = camGo.AddComponent<Camera>();
        studioCam.enabled = false;
        studioCam.clearFlags = CameraClearFlags.SolidColor;
        studioCam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        studioCam.orthographic = true;
        studioCam.orthographicSize = 0.98f;
        studioCam.nearClipPlane = 0.1f;
        studioCam.farClipPlane = 30f;
        studioCam.allowHDR = false;
        studioCam.targetTexture = rt;
        camGo.transform.localPosition = new Vector3(0f, 0f, -6f);

        GameObject lightGo = new GameObject("Light");
        lightGo.transform.SetParent(studioRoot.transform, false);
        lightGo.transform.localPosition = new Vector3(2.5f, 3f, -5f);
        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = 30f;
        light.intensity = 6f;
        light.shadows = LightShadows.None;
    }

    private void Cleanup()
    {
        if (canvasRoot != null) Destroy(canvasRoot);
        if (studioRoot != null) Destroy(studioRoot);
        if (rt != null) { rt.Release(); Destroy(rt); }
        canvasRoot = null; studioRoot = null; rt = null; bits = null; rings = null; lines = null; cubeImage = null;
        clouds = segs = feathers = fist = null; sun = sphere = glowBehind = null;
    }
}
