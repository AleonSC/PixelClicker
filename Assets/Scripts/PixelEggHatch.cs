using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The egg's hatching show (added by <c>PixelClicker.Awake</c>; called by <see cref="PixelEgg"/> on the last click): the screen goes dark, the egg
/// zooms up to the middle and shakes harder and harder while light leaks out of its cracks, the top pops off in a flash, the new pet sits inside,
/// hops out and jumps about in a rain of sparkles, then hops into the backpack icon like a Dragon Cube does. The egg and pet are drawn by a hidden
/// studio camera into a RenderTexture on a UI image (1 studio unit = 100 canvas units). Unscaled time; Escape skips to the end. The pet is awarded
/// when the show finishes (<c>onDone</c>).
/// </summary>
public class PixelEggHatch : MonoBehaviour
{
    [Header("General")]
    [Tooltip("Turn the show off (the egg then hatches at once, as before).")]
    [SerializeField] private bool disableShow = false;

    [Header("Timing (seconds)")]
    [Tooltip("The screen darkens and the egg zooms up to the middle.")]
    [SerializeField] private float zoomSeconds = 0.8f;
    [Tooltip("The egg shakes harder and harder and its cracks glow.")]
    [SerializeField] private float shakeSeconds = 2.4f;
    [Tooltip("After the top pops off the pet sits in the shell for this long.")]
    [SerializeField] private float peekSeconds = 0.6f;
    [Tooltip("The pet hops out and jumps around.")]
    [SerializeField] private float jumpSeconds = 2.6f;
    [Tooltip("The pet hops into the backpack.")]
    [SerializeField] private float stashSeconds = 0.8f;
    [Tooltip("The screen brightens again.")]
    [SerializeField] private float fadeOutSeconds = 0.5f;

    [Header("Look")]
    [Tooltip("How dark the screen gets (0 = not at all, 1 = black).")]
    [SerializeField] [Range(0f, 1f)] private float darkness = 0.88f;
    [Tooltip("Edge of the egg at its biggest, in canvas units.")]
    [SerializeField] private float eggSize = 380f;
    [Tooltip("Edge of the pet while it jumps around, in canvas units.")]
    [SerializeField] private float petSize = 230f;
    [Tooltip("Size of the backpack icon (canvas units); matches the Ghost's.")]
    [SerializeField] private float backpackSize = 130f;
    [Tooltip("Distance of the backpack icon from the right edge and the bottom bar (canvas units); matches the Ghost's.")]
    [SerializeField] private Vector2 backpackMargin = new Vector2(40f, 40f);
    [Tooltip("Sparkle colour.")]
    [SerializeField] private Color sparkleColor = new Color(1f, 0.92f, 0.6f, 1f);
    [Tooltip("Sparkles shed per second while the pet jumps.")]
    [SerializeField] private float sparklesPerSecond = 60f;
    [Tooltip("Heading text.")]
    [SerializeField] private string headingText = "It hatched!";
    [Tooltip("Text under the heading once the pet is out. {0} = the pet's name.")]
    [SerializeField] private string petFormat = "A new friend: {0}";

    private class ChipBody { public Transform t; public Vector3 vel, spin; }

    private static PixelEggHatch instance;
    private bool playing, skipRequested;
    private PixelClicker clicker;

    private GameObject canvasRoot;
    private RectTransform canvasRect;
    private Image dim, flash;
    private Image backpack;
    private RectTransform backpackRect;
    private RawImage picture;
    private TMP_Text title, subtitle;
    private readonly PixelUISparks sparkles = new PixelUISparks();   // the gold screen sparkles

    private GameObject studioRoot;
    private Camera studioCam;
    private RenderTexture rt;
    private Material shellMat;
    private Transform shellPart, lidMesh, wholePart;
    private Transform eggRoot, lidPart, petPivot, petModel;
    private readonly List<Transform> cracks = new List<Transform>();
    private readonly List<ChipBody> chips = new List<ChipBody>();

    public static bool Playing => instance != null && instance.playing;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; }

    private void Awake()
    {
        instance = this;
        PixelWindows.Register(this, 165, () => playing, () => skipRequested = true);   // Escape skips the show   // escape-only: skips the show
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        PixelWindows.Unregister(this);
        Cleanup();
    }

    /// <summary>Starts the show. False if it can't play (the caller then hatches the egg at once).</summary>
    public static bool Play(PixelClicker owner, int petTier, string petName, Vector3 eggWorldPosition, System.Action onDone)
    {
        if (instance == null || instance.disableShow || instance.playing || owner == null || !owner.IsValidTierIndex(petTier)) return false;
        instance.clicker = owner;
        instance.playing = true;
        instance.StartCoroutine(instance.PlayOne(petTier, petName, eggWorldPosition, onDone));
        return true;
    }

    private static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
    private static float EaseOut(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t) * (1f - t); }

    private Vector2 ToStudio(Vector2 canvasPos, Vector2 canvasSize) => (canvasPos - canvasSize * 0.5f) / 100f;

    private System.Collections.IEnumerator PlayOne(int petTier, string petName, Vector3 eggWorld, System.Action onDone)
    {
        Build(petTier);
        if (picture == null) { Cleanup(); playing = false; onDone?.Invoke(); yield break; }
        yield return null;   // the canvas scaler sets the canvas size on the first frame

        Vector2 size = canvasRect.rect.size;
        SetupCamera(size);
        Camera cam = clicker.ViewCamera;
        Vector2 screen = cam != null ? (Vector2)cam.WorldToScreenPoint(eggWorld) : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector2 startC = new Vector2(Mathf.Clamp(screen.x / Screen.width * size.x, 0f, size.x), Mathf.Clamp(screen.y / Screen.height * size.y, 0f, size.y));
        Vector2 start = ToStudio(startC, size);
        Vector2 centre = new Vector2(0f, size.y * 0.02f / 100f);
        float bar = PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f;
        Vector2 packC = new Vector2(size.x - backpackMargin.x - backpackSize * 0.5f, bar + backpackMargin.y + backpackSize * 0.5f);
        backpackRect.anchoredPosition = packC;
        Vector2 pack = ToStudio(packC, size);

        float S = eggSize / 100f, P = petSize / 100f;
        float rim = S * 0.05f;
        float groundY = centre.y - S * 0.5f + P * 0.5f;      // the pet's centre height when standing where the egg stood
        Vector2 inside = new Vector2(centre.x, centre.y + rim + P * 0.18f);

        title.text = headingText;
        subtitle.text = "";
        PixelAudio.Play("dragon_find");

        float zoomT = Mathf.Max(0.05f, zoomSeconds), shakeT = Mathf.Max(0.2f, shakeSeconds), peekT = Mathf.Max(0.05f, peekSeconds);
        float jumpT = Mathf.Max(0.5f, jumpSeconds), stashT = Mathf.Max(0.05f, stashSeconds), fadeT = Mathf.Max(0.05f, fadeOutSeconds);
        float tPop = zoomT + shakeT, tHop = tPop + peekT, tStash = tHop + jumpT, tEnd = tStash + stashT;

        // Hop targets for the jump around.
        int hops = Mathf.Max(2, Mathf.RoundToInt(jumpT / 0.55f));
        float reach = Mathf.Min(size.x / 200f * 0.6f, 6.5f);
        List<Vector2> spots = new List<Vector2>();
        float sideSign = Random.value < 0.5f ? -1f : 1f;
        float minX = S * 0.5f + P * 0.5f + 0.3f;   // never land on the shell
        reach = Mathf.Max(reach, minX + 0.6f);
        for (int i = 0; i < hops; i++)
        {
            spots.Add(new Vector2(centre.x + sideSign * Mathf.Lerp(minX, reach, Random.value), groundY));
            sideSign = -sideSign;
        }

        float t = 0f, bump = 0f, flashAmt = 0f;
        bool popped = false, stashed = false, named = false;
        Vector3 lidVel = Vector3.zero, lidSpin = Vector3.zero;
        Vector2 lastPetPos = inside;

        while (t < tEnd + fadeT)
        {
            float dt = Time.unscaledDeltaTime;
            if (skipRequested)
            {
                skipRequested = false;
                if (t < tEnd) t = tEnd;
                if (!popped) { popped = true; KillShell(); }
                named = true;
            }
            t += dt;
            float darkAmount, packAlpha, textAlpha;
            Vector2 petPos = lastPetPos;
            float petScale = 0f;
            Vector3 petEuler = Vector3.zero, modelScale = Vector3.one;

            if (t < tPop)
            {
                // Egg: zooms in, then shakes with growing force while the cracks fill with light.
                float zk = Smooth(t / zoomT);
                float sk = Mathf.Clamp01((t - zoomT) / shakeT);
                Vector2 pos = Vector2.Lerp(start, centre, EaseOut(t / zoomT));
                float scale = Mathf.Lerp(0.25f, 1f, EaseOut(t / zoomT));
                float amp = sk * sk;
                eggRoot.position = studioRoot.transform.position + new Vector3(pos.x + Mathf.Sin(t * 57f) * 0.07f * amp * S, pos.y + Mathf.Abs(Mathf.Sin(t * 33f)) * 0.05f * amp * S, 0f);
                eggRoot.localScale = Vector3.one * scale * (1f + 0.03f * Mathf.Sin(t * 20f) * amp);
                eggRoot.localRotation = Quaternion.Euler(Mathf.Sin(t * 29f) * 5f * amp, 0f, Mathf.Sin(t * 41f) * 9f * amp + Mathf.Sin(t * 5f) * 3f * sk);
                int visible = Mathf.RoundToInt(cracks.Count * Mathf.Clamp01(sk * 1.15f));
                for (int i = 0; i < cracks.Count; i++) cracks[i].gameObject.SetActive(i < visible);
                darkAmount = Smooth(t / (zoomT * 0.9f));
                textAlpha = Smooth((t - zoomT * 0.5f) / 0.5f) * (1f - Smooth((t - tPop + 0.3f) / 0.3f));
                packAlpha = 0f;
                if (sk > 0.5f && Random.value < dt * 14f * sk) ThrowChip(eggRoot.position, S * 0.05f);
                if (sk > 0.3f && Random.value < dt * 9f * sk) PixelAudio.Play("hit");
                petPos = inside; petScale = 0f;
            }
            else
            {
                if (!popped)
                {
                    popped = true;
                    flashAmt = 1f;
                    PixelAudio.Play("achievement");
                    // The lid flies off, shell chips burst out, the pet is revealed.
                    wholePart.gameObject.SetActive(false);
                    shellPart.gameObject.SetActive(true);
                    lidMesh.gameObject.SetActive(true);
                    lidPart.SetParent(studioRoot.transform, true);
                    lidVel = new Vector3(Random.Range(-1.5f, 1.5f), 9f, -1f);
                    lidSpin = new Vector3(Random.Range(-200f, 200f), Random.Range(-120f, 120f), Random.Range(-260f, 260f));
                    for (int i = 0; i < 14; i++) ThrowChip(eggRoot.position + new Vector3(0f, rim, 0f), S * 0.12f, true);
                    eggRoot.localRotation = Quaternion.identity;
                    for (int i = 0; i < cracks.Count; i++) cracks[i].gameObject.SetActive(true);
                    sparkles.Burst(CanvasOf(centre + new Vector2(0f, S * 0.3f), size), 60, 160f, 600f, 1f);
                }
                darkAmount = 1f; packAlpha = 1f; textAlpha = 1f;
                eggRoot.position = studioRoot.transform.position + new Vector3(centre.x, centre.y, 0f);
                eggRoot.localScale = Vector3.one;

                if (t < tHop)
                {
                    float d = (t - tPop) / peekT;
                    petPos = Vector2.Lerp(inside, inside + new Vector2(0f, P * 0.25f), EaseOut(d));
                    petScale = P * (0.9f + 0.1f * Smooth(d));
                    petEuler = new Vector3(0f, Mathf.Sin(d * 6f) * 18f, Mathf.Sin(d * 9f) * 6f);
                }
                else if (t < tStash)
                {
                    float d = (t - tHop) / jumpT;
                    float hopF = d * hops;
                    int hi = Mathf.Min(hops - 1, Mathf.FloorToInt(hopF));
                    float k = hopF - hi;
                    Vector2 from = hi == 0 ? inside + new Vector2(0f, P * 0.25f) : spots[hi - 1];
                    Vector2 to = spots[hi];
                    float height = hi == 0 ? P * 1.3f : P * 0.9f;
                    petPos = Vector2.Lerp(from, to, k) + new Vector2(0f, Mathf.Sin(k * Mathf.PI) * height);
                    if (hi == 0 && k < 0.5f) petPos.y = Mathf.Max(petPos.y, inside.y);
                    petScale = P;
                    float stretch = 1f + 0.12f * Mathf.Sin(k * Mathf.PI);
                    petEuler = new Vector3(0f, 360f * k + hi * 40f, Mathf.Sin(k * Mathf.PI * 2f) * 10f);
                    sparkles.Emit(CanvasOf(petPos, size), petScale * 100f, sparklesPerSecond, dt);
                    modelScale = new Vector3(1f / Mathf.Sqrt(stretch), stretch, 1f / Mathf.Sqrt(stretch));
                    if (!named) { named = true; subtitle.text = string.Format(petFormat, petName); }
                    lastPetPos = petPos;
                }
                else if (t < tEnd)
                {
                    float d = (t - tStash) / stashT;
                    float k = d * d * (3f - 2f * d);
                    Vector2 from = spots[hops - 1];
                    petPos = Vector2.Lerp(from, pack, k) + new Vector2(0f, Mathf.Sin(k * Mathf.PI) * size.y / 100f * 0.14f);
                    petScale = Mathf.Lerp(P, backpackSize / 100f * 0.35f, k);
                    petEuler = new Vector3(20f * k, 360f * k + 90f, 0f);
                    sparkles.Emit(CanvasOf(petPos, size), petScale * 80f, sparklesPerSecond, dt);
                    lastPetPos = petPos;
                }
                else
                {
                    if (!stashed) { stashed = true; sparkles.Burst(packC, 40, 90f, 380f, 0.8f); PixelAudio.Play("dragon_stash"); bump = 1f; }
                    float f = (t - tEnd) / fadeT;
                    darkAmount = 1f - Smooth(f); packAlpha = 1f - Smooth(f * 1.2f); textAlpha = 1f - Smooth(f * 1.6f);
                    petScale = 0f;
                }
                if (petModel != null) petModel.localScale = modelScale;
                ApplyPet(petPos, petScale, petEuler);
            }
            if (popped && shellPart != null && t >= tHop)
            {
                float crumble = Mathf.Clamp01((t - tHop) / 0.35f);   // the empty shell shrinks away as the pet hops out
                shellPart.localScale = new Vector3(S, S * 0.55f, S) * (1f - Smooth(crumble));
                if (crumble >= 1f && shellPart.gameObject.activeSelf)
                {
                    shellPart.gameObject.SetActive(false);
                    foreach (Transform crack in cracks) if (crack != null && crack.parent == eggRoot) crack.gameObject.SetActive(false);   // the cracks went with it
                }
            }
            AnimateDebris(dt, ref lidVel, lidSpin, popped);
            bump = Mathf.Max(0f, bump - dt * 3.5f);
            flashAmt = Mathf.Max(0f, flashAmt - dt * 3.5f);
            Color c = dim.color; c.a = darkness * darkAmount; dim.color = c;
            c = flash.color; c.a = flashAmt * 0.85f; flash.color = c;
            c = backpack.color; c.a = packAlpha; backpack.color = c;
            backpackRect.localScale = Vector3.one * (1f + 0.35f * bump);
            title.alpha = textAlpha; subtitle.alpha = textAlpha;
            studioCam.Render();
            sparkles.Tick(dt);
            yield return null;
        }

        float tail = 0.5f;
        while (tail > 0f) { tail -= Time.unscaledDeltaTime; sparkles.Tick(Time.unscaledDeltaTime); yield return null; }
        Cleanup();
        playing = false;
        onDone?.Invoke();
    }

    private Vector2 CanvasOf(Vector2 studio, Vector2 size) => studio * 100f + size * 0.5f;

    private void ApplyPet(Vector2 pos, float scale, Vector3 euler)
    {
        bool show = scale > 0.01f;
        if (petPivot.gameObject.activeSelf != show) petPivot.gameObject.SetActive(show);
        if (!show) return;
        petPivot.position = studioRoot.transform.position + new Vector3(pos.x, pos.y, -0.6f);   // a little towards the camera so it shows over the shell's rim
        petPivot.localScale = Vector3.one * scale;
        petPivot.localRotation = Quaternion.Euler(euler + new Vector3(18f, 0f, 0f));
    }

    private void SetupCamera(Vector2 size)
    {
        studioCam.orthographicSize = size.y / 200f;
        studioCam.aspect = size.x / size.y;
        rt = new RenderTexture(Mathf.Max(64, Mathf.RoundToInt(size.x * 0.67f)), Mathf.Max(64, Mathf.RoundToInt(size.y * 0.67f)), 24, RenderTextureFormat.ARGB32) { name = "Egg Hatch Picture" };
        studioCam.targetTexture = rt;
        picture.texture = rt;
        picture.enabled = true;
    }

    // ---- shell debris ----

    private void ThrowChip(Vector3 at, float size, bool burst = false)
    {
        GameObject g = new GameObject("Shell Chip", typeof(MeshFilter), typeof(MeshRenderer));
        g.transform.SetParent(studioRoot.transform, true);
        g.transform.position = at + Random.insideUnitSphere * size;
        g.transform.rotation = Random.rotation;
        g.transform.localScale = Vector3.one * size * Random.Range(0.6f, 1.4f);
        g.GetComponent<MeshFilter>().sharedMesh = PixelEgg.RoundedCubeMesh(0.2f, 3);
        if (shellMat != null) g.GetComponent<MeshRenderer>().sharedMaterial = shellMat;
        float power = burst ? Random.Range(4f, 9f) : Random.Range(1.5f, 3.5f);
        Vector3 dir = new Vector3(Random.Range(-1f, 1f), Random.Range(0.3f, 1f), Random.Range(-0.4f, 0.2f)).normalized;
        chips.Add(new ChipBody { t = g.transform, vel = dir * power, spin = Random.onUnitSphere * Random.Range(200f, 600f) });
    }

    private void AnimateDebris(float dt, ref Vector3 lidVel, Vector3 lidSpin, bool popped)
    {
        if (popped && lidPart != null && lidPart.parent == studioRoot.transform)
        {
            lidVel += Vector3.down * 24f * dt;
            lidPart.position += lidVel * dt;
            lidPart.Rotate(lidSpin * dt, Space.Self);
            if (lidPart.localPosition.y < -14f) lidPart.gameObject.SetActive(false);
        }
        for (int i = chips.Count - 1; i >= 0; i--)
        {
            ChipBody c = chips[i];
            if (c.t == null) { chips.RemoveAt(i); continue; }
            c.vel += Vector3.down * 20f * dt;
            c.t.position += c.vel * dt;
            c.t.Rotate(c.spin * dt, Space.Self);
            if (c.t.localPosition.y < -14f) { Destroy(c.t.gameObject); chips.RemoveAt(i); }
        }
    }

    private void KillShell()
    {
        if (lidPart != null) lidPart.gameObject.SetActive(false);
    }

    // ---- building ----

    private static Image NewImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private void Build(int petTier)
    {
        Cleanup();
        TMP_FontAsset font = clicker.UIFont;
        canvasRoot = PixelUIKit.CreateCanvas("Egg Hatch", 691, new Vector2(1920f, 1080f), true);
        canvasRect = canvasRoot.GetComponent<RectTransform>();

        dim = NewImage("Dark", canvasRoot.transform, new Color(0f, 0f, 0f, 0f));
        PixelUIKit.Stretch(dim.rectTransform);
        dim.raycastTarget = true;   // blocks clicks while it plays

        backpack = NewImage("Backpack", canvasRoot.transform, new Color(1f, 1f, 1f, 0f));
        backpack.sprite = PixelGhostMinigame.BuildBackpackSprite();
        backpack.preserveAspect = true;
        backpackRect = backpack.rectTransform;
        backpackRect.anchorMin = backpackRect.anchorMax = new Vector2(0f, 0f);
        backpackRect.pivot = new Vector2(0.5f, 0.5f);
        backpackRect.sizeDelta = new Vector2(backpackSize, backpackSize);

        GameObject raw = new GameObject("Studio Picture", typeof(RectTransform), typeof(RawImage));
        raw.transform.SetParent(canvasRoot.transform, false);
        picture = raw.GetComponent<RawImage>();
        picture.raycastTarget = false;
        picture.enabled = false;
        PixelUIKit.Stretch(raw.GetComponent<RectTransform>());

        flash = NewImage("Flash", canvasRoot.transform, new Color(1f, 1f, 0.9f, 0f));
        PixelUIKit.Stretch(flash.rectTransform);

        float bar = PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f;
        title = PixelUIKit.CreateText(font, canvasRoot.transform, "Title", "", 64f, TextAlignmentOptions.Center, FontStyles.Bold, new Color(1f, 0.92f, 0.6f, 1f));
        PixelUIKit.Caps(title);
        RectTransform tr = title.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f); tr.pivot = new Vector2(0.5f, 1f);
        tr.sizeDelta = new Vector2(0f, 100f); tr.anchoredPosition = new Vector2(0f, -(bar + 40f));
        subtitle = PixelUIKit.CreateText(font, canvasRoot.transform, "Subtitle", "", 40f, TextAlignmentOptions.Center, FontStyles.Bold, Color.white);
        RectTransform sr = subtitle.rectTransform;
        sr.anchorMin = new Vector2(0f, 1f); sr.anchorMax = new Vector2(1f, 1f); sr.pivot = new Vector2(0.5f, 1f);
        sr.sizeDelta = new Vector2(0f, 70f); sr.anchoredPosition = new Vector2(0f, -(bar + 140f));

        sparkles.Build(canvasRoot.transform, 160, sparkleColor);
        BuildStudio(petTier);
    }

    private void BuildStudio(int petTier)
    {
        float S = eggSize / 100f, P = petSize / 100f;
        studioRoot = PixelShowStudio.Create("Egg Hatch Studio", -4500f, 14f, 40f, new Vector3(2f, 4f, -10f), 80f, 22f, out studioCam);

        // Egg: a lower shell and a lid, both rounded cubes, with glowing cracks on the front.
        eggRoot = new GameObject("Egg").transform;
        eggRoot.SetParent(studioRoot.transform, false);
        Mesh mesh = PixelEgg.RoundedCubeMesh(0.16f, 6);
        Material shell = clicker.CreateVisualMaterial(new Color(0.99f, 0.99f, 0.97f, 1f), false);
        shellMat = shell;
        wholePart = NewPart("Whole Egg", eggRoot, mesh, shell, Vector3.one * S, Vector3.zero);   // one piece until it pops, so no seam shows
        shellPart = NewPart("Shell", eggRoot, mesh, shell, new Vector3(S, S * 0.55f, S), new Vector3(0f, -S * 0.225f, 0f));
        lidPart = new GameObject("Lid Holder").transform;   // unscaled, so the cracks on it keep their shape
        lidPart.SetParent(eggRoot, false);
        lidMesh = NewPart("Lid", lidPart, mesh, shell, new Vector3(S, S * 0.45f, S), new Vector3(0f, S * 0.275f, 0f));
        shellPart.gameObject.SetActive(false);
        lidMesh.gameObject.SetActive(false);

        Material glow = clicker.CreateVisualMaterial(new Color(1f, 0.93f, 0.55f, 1f), false);
        Random.State saved = Random.state;
        Random.InitState(12345);
        for (int i = 0; i < 18; i++)
        {
            float y = Random.Range(-0.4f, 0.4f) * S;
            GameObject c = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(c.GetComponent<Collider>());
            c.name = "Crack";
            c.transform.SetParent(y > S * 0.05f ? lidPart : eggRoot, false);
            c.transform.localPosition = new Vector3(Random.Range(-0.38f, 0.38f) * S, y, -S * 0.5f - 0.03f);
            c.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 180f));
            c.transform.localScale = new Vector3(Random.Range(0.25f, 0.6f) * S * 0.5f, 0.07f, 0.04f);
            Renderer r = c.GetComponent<Renderer>();
            if (glow != null) r.sharedMaterial = glow;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            c.SetActive(false);
            cracks.Add(c.transform);
        }
        Random.state = saved;

        // The pet: a display model of its pixel type on a pivot (scaled by 'P' units edge).
        petPivot = new GameObject("Pet Pivot").transform;
        petPivot.SetParent(studioRoot.transform, false);
        GameObject model = clicker.CreateDisplayPixel(petTier, petPivot, 1f);
        if (model == null) { model = GameObject.CreatePrimitive(PrimitiveType.Cube); model.transform.SetParent(petPivot, false); }
        foreach (Collider col in model.GetComponentsInChildren<Collider>()) Destroy(col);
        model.transform.localPosition = Vector3.zero;
        petModel = model.transform;
        petPivot.gameObject.SetActive(false);
    }

    private Transform NewPart(string name, Transform parent, Mesh mesh, Material mat, Vector3 scale, Vector3 localPos)
    {
        GameObject g = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        g.transform.localScale = scale;
        g.GetComponent<MeshFilter>().sharedMesh = mesh;
        if (mat != null) g.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return g.transform;
    }

    private void Cleanup()
    {
        if (canvasRoot != null) Destroy(canvasRoot);
        PixelShowStudio.Release(ref studioRoot, ref rt);
        canvasRoot = null; sparkles.Drop(); picture = null;
        cracks.Clear(); chips.Clear();
    }
}
