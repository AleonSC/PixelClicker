using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static PixelInput;

/// <summary>
/// The message in a bottle (Fishing). When the bottle breaks, the glass bursts, a rolled-up scroll flies to the middle of the screen and unrolls into a
/// treasure map (drawn in code: the island, a dotted path from the cube and a big red X), then the map shrinks into the spot it marks and an X appears on the
/// ground somewhere in view. Click the X several times to dig; the last dig unearths the treasure: a haul of good fishing pixels (mostly bait).
/// Everything is tunable in the Inspector; right-clicking the bottle skips all of it (see PixelClicker.DismissBottle).
/// </summary>
public class PixelTreasureMap : MonoBehaviour
{
    [Header("Treasure map")]
    [SerializeField, Tooltip("Turns the whole treasure map off: breaking a bottle then only pays its small value.")]
    private bool disableMap;
    [SerializeField, Tooltip("Clicks it takes to dig the treasure up. 0 = 6.")]
    private int digClicks;
    [SerializeField, Tooltip("Pixels in the treasure haul (a few more or fewer are rolled on top). 0 = 10.")]
    private int haulPixels;
    [SerializeField, Tooltip("Seconds an X stays on the ground before it fades away. 0 = 300.")]
    private float markSeconds;
    [SerializeField, Tooltip("How close (screen pixels) a click must be to the X to dig. 0 = 80.")]
    private float clickRadiusPixels;
    [SerializeField, Tooltip("Most X marks on the ground at once (the oldest fades when there are more). 0 = 3.")]
    private int maxMarks;
    [SerializeField, Tooltip("Seconds the opened map stays in the middle of the screen. 0 = 1.8.")]
    private float readSeconds;
    [SerializeField, Tooltip("Ground area for the X, in pixel widths from the cube: nearest. 0 = 3.")]
    private float spotNearest;
    [SerializeField, Tooltip("Ground area for the X, in pixel widths from the cube: farthest. 0 = 7.")]
    private float spotFarthest;

    private static PixelTreasureMap instance;

    /// <summary>True while the mouse is over an X (the cube ignores the click then).</summary>
    public static bool Hot { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; Hot = false; }

    private class Mark
    {
        public GameObject root;
        public Transform xRoot, mound;
        public Vector3 pos;
        public int total, left;
        public float born, nextRing, shake;
        public bool dying;
    }

    private readonly List<Mark> marks = new List<Mark>();
    private readonly Queue<Vector3> queue = new Queue<Vector3>();
    private PixelClicker clicker;
    private bool showing;
    private float lastDig;
    private Material redMat, dirtMat;

    private void Awake() { instance = this; }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        Hot = false;
        foreach (Mark m in marks) if (m.root != null) Destroy(m.root);
    }

    /// <summary>A bottle broke at 'from': play the map show (one at a time; more bottles wait their turn).</summary>
    public static void Begin(PixelClicker c, Vector3 from)
    {
        if (instance == null) return;
        instance.clicker = c;
        PixelAudio.Play("bottle_break");
        PixelStats.Count("bottles.broken");
        if (instance.disableMap) return;
        instance.queue.Enqueue(from);
    }

    // ------------------------------------------------------------------
    // The X marks on the ground
    // ------------------------------------------------------------------

    private void Update()
    {
        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
        if (clicker == null) return;
        if (!showing && queue.Count > 0) StartCoroutine(Show(queue.Dequeue()));

        Camera cam = clicker.ViewCamera;
        bool blocked = cam == null || PixelCalm.InMenu || (Time.timeScale <= 0f && !PixelTimeStop.IsStopped) || PointerOverUI() || PixelClicker.GodMode;
        Mark hovered = null;
        float radius = clickRadiusPixels > 0f ? clickRadiusPixels : 80f;
        float best = float.MaxValue;
        for (int i = marks.Count - 1; i >= 0; i--)
        {
            Mark m = marks[i];
            if (m.root == null) { marks.RemoveAt(i); continue; }
            Animate(m);
            if (m.dying || blocked) continue;
            Vector3 sp = cam.WorldToScreenPoint(m.pos);
            if (sp.z <= 0f) continue;
            float d = Vector2.Distance(new Vector2(sp.x, sp.y), PointerPosition());
            if (d < radius && d < best) { best = d; hovered = m; }
        }
        Hot = hovered != null;
        if (Hot) PixelPointerClaim.Claim();
        if (hovered != null && LeftPressed() && Time.unscaledTime - lastDig > 0.1f)
        {
            lastDig = Time.unscaledTime;
            Dig(hovered);
        }
    }

    private void Animate(Mark m)
    {
        float size = clicker.PixelBaseSize;
        float t = Time.unscaledTime;
        if (!m.dying)
        {
            float k = 1f - m.left / (float)Mathf.Max(1, m.total);
            float pulse = 1f + 0.07f * Mathf.Sin(t * 4f) + m.shake;
            m.shake = Mathf.MoveTowards(m.shake, 0f, Time.unscaledDeltaTime * 1.2f);
            if (m.xRoot != null) m.xRoot.localScale = Vector3.one * pulse * (1f - 0.65f * k);
            if (m.mound != null)
            {
                float w = size * (0.25f + 0.85f * k);
                m.mound.localScale = new Vector3(w, size * (0.04f + 0.3f * k), w);
                m.mound.gameObject.SetActive(k > 0.01f);
            }
            if (t >= m.nextRing)
            {
                m.nextRing = t + 2.2f;
                PixelFxRing.Spawn(m.pos + Vector3.up * size * 0.05f, new Color(1f, 0.35f, 0.25f, 0.8f), size * 0.35f, size * 1.7f, 0.9f, 0f);
            }
            if (Time.time - m.born > (markSeconds > 0f ? markSeconds : 300f)) Fade(m);
        }
    }

    private void Fade(Mark m)
    {
        m.dying = true;
        StartCoroutine(Shrink(m, 0.5f));
    }

    private IEnumerator Shrink(Mark m, float seconds)
    {
        float t = 0f;
        Vector3 start = m.root != null ? m.root.transform.localScale : Vector3.one;
        while (t < seconds && m.root != null)
        {
            t += Time.unscaledDeltaTime;
            m.root.transform.localScale = start * Mathf.Clamp01(1f - t / seconds);
            yield return null;
        }
        if (m.root != null) Destroy(m.root);
        marks.Remove(m);
    }

    private void Dig(Mark m)
    {
        float size = clicker.PixelBaseSize;
        m.left--;
        m.shake = 0.18f;
        PixelAudio.Play("dig_x");
        DirtFx(m.pos, size, m.left <= 0 ? 1.6f : 1f);
        if (m.left > 0) return;

        // The last dig: the treasure comes up.
        m.dying = true;
        PixelAudio.Play("dig_done");
        GoldFx(m.pos, size);
        int baseCount = haulPixels > 0 ? haulPixels : 10;
        int count = Mathf.Max(3, baseCount + Random.Range(-2, 5));
        int given = clicker.GiveFishingHaul(m.pos + Vector3.up * size * 0.2f, count);
        PixelStats.Count("treasure.dug");
        PixelHints.Announce(given > 0 ? "You dug up buried treasure: " + given + " pixels!" : "You dug up the treasure, but the chest was empty");
        if (m.xRoot != null) m.xRoot.gameObject.SetActive(false);
        StartCoroutine(LingerThenShrink(m));
    }

    private IEnumerator LingerThenShrink(Mark m)
    {
        yield return new WaitForSecondsRealtime(1.6f);
        yield return Shrink(m, 0.6f);
    }

    private static void Burst(Transform parent, string name, Vector2 life, Vector2 speed, Vector2 psize, float gravity, Color a, Color b, int count, float radius, float size, bool fade = false)
    {
        PixelFx.Spec s = new PixelFx.Spec
        {
            lifetime = life, speed = speed * size, size = psize * size, gravity = gravity, colorA = a, colorB = b, burst = count,
            shape = ParticleSystemShapeType.Sphere, shapeRadius = size * radius, randomDirection = 0.1f, maxParticles = count + 10,
        };
        if (fade) s.overLife = PixelFx.FadeGradient(Color.white, Color.white, Color.white, 0.85f);
        PixelFx.Make(parent, name, s).Play();
    }

    private static Transform FxRoot(Vector3 at)
    {
        GameObject go = new GameObject("Treasure Fx");
        go.transform.position = at;
        Destroy(go, 3.5f);
        return go.transform;
    }

    private static void DirtFx(Vector3 at, float size, float scale)
    {
        if (PixelFx.Material == null) return;
        Transform t = FxRoot(at + Vector3.up * size * 0.05f);
        Burst(t, "Clods", new Vector2(0.6f, 1.1f), new Vector2(1.6f, 4f) * scale, new Vector2(0.07f, 0.16f), 2.8f, new Color(0.45f, 0.3f, 0.17f), new Color(0.28f, 0.18f, 0.1f), Mathf.RoundToInt(16 * scale), 0.25f, size);
        Burst(t, "Dust", new Vector2(0.9f, 1.5f), new Vector2(0.2f, 1f), new Vector2(0.25f, 0.5f), -0.04f, new Color(0.62f, 0.5f, 0.38f, 0.5f), new Color(0.5f, 0.4f, 0.3f, 0.4f), 8, 0.4f, size, true);
    }

    private static void GoldFx(Vector3 at, float size)
    {
        if (PixelFx.Material == null) return;
        Transform t = FxRoot(at + Vector3.up * size * 0.1f);
        Burst(t, "Coins", new Vector2(0.8f, 1.5f), new Vector2(3f, 6.5f), new Vector2(0.07f, 0.15f), 2.4f, new Color(1f, 0.9f, 0.3f), new Color(1f, 0.7f, 0.15f), 40, 0.25f, size);
        Burst(t, "Glints", new Vector2(0.8f, 1.6f), new Vector2(0.8f, 3.4f), new Vector2(0.05f, 0.13f), -0.1f, new Color(1f, 1f, 0.85f), new Color(1f, 0.85f, 0.4f), 36, 0.5f, size, true);
        PixelFxRing.Spawn(at + Vector3.up * size * 0.05f, new Color(1f, 0.85f, 0.3f, 0.9f), size * 0.4f, size * 4.5f, 0.8f, 3f);
    }

    // ------------------------------------------------------------------
    // The show: glass bursts, the scroll flies in, unrolls, shrinks into the spot
    // ------------------------------------------------------------------

    private IEnumerator Show(Vector3 from)
    {
        showing = true;
        float size = clicker.PixelBaseSize;
        Camera cam = clicker.ViewCamera;
        if (cam == null) { showing = false; yield break; }

        Vector3 spot;
        if (!PickSpot(cam, size, out spot)) spot = clicker.CubeBasePosition + cam.transform.right * size * 3.5f;   // (no ground found: beside the cube)
        Vector3 cubeVp = cam.WorldToViewportPoint(clicker.CubeBasePosition);
        Vector3 spotVp = cam.WorldToViewportPoint(spot);
        Texture2D mapTex = MakeMapTexture(new Vector2(cubeVp.x, cubeVp.y), new Vector2(spotVp.x, spotVp.y));

        GlassFx(from, size);

        // ---- the UI ----
        GameObject canvasObject = PixelUIKit.CreateCanvas("Treasure Map", 660, new Vector2(1920f, 1080f), false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        float scale = canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
        TMP_FontAsset font = clicker.UIFont;

        Image dim = NewImage(canvasObject.transform, "Dim", new Color(0f, 0f, 0f, 0f));
        PixelUIKit.Stretch(dim.rectTransform);

        RectTransform group = new GameObject("Scroll", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
        group.SetParent(canvasObject.transform, false);
        group.anchorMin = group.anchorMax = Vector2.zero;
        group.pivot = new Vector2(0.5f, 0.5f);
        group.sizeDelta = new Vector2(700f, 470f);
        CanvasGroup groupAlpha = group.GetComponent<CanvasGroup>();

        const float PaperW = 700f, PaperH = 470f;
        RectTransform mask = new GameObject("Mask", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
        mask.SetParent(group, false);
        mask.anchorMin = mask.anchorMax = new Vector2(0.5f, 0.5f);
        mask.sizeDelta = new Vector2(PaperW, 0f);
        RawImage paper = new GameObject("Paper", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        paper.transform.SetParent(mask, false);
        paper.texture = mapTex;
        paper.raycastTarget = false;
        paper.rectTransform.anchorMin = paper.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        paper.rectTransform.sizeDelta = new Vector2(PaperW, PaperH);

        Image topRoller = Roller(group, "Top Roller", PaperW + 70f);
        Image bottomRoller = Roller(group, "Bottom Roller", PaperW + 70f);

        TMP_Text title = PixelUIKit.CreateText(font, canvasObject.transform, "Title", "TREASURE MAP", 64f, TextAlignmentOptions.Center, FontStyles.Bold, new Color(1f, 0.92f, 0.6f, 0f));
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.sizeDelta = new Vector2(1000f, 90f);
        titleRect.anchoredPosition = new Vector2(0f, -(PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f) - 40f);
        TMP_Text sub = PixelUIKit.CreateText(font, canvasObject.transform, "Sub", "X marks the spot!", 34f, TextAlignmentOptions.Center, FontStyles.Italic, new Color(1f, 1f, 1f, 0f));
        RectTransform subRect = sub.rectTransform;
        subRect.anchorMin = subRect.anchorMax = new Vector2(0.5f, 1f);
        subRect.pivot = new Vector2(0.5f, 1f);
        subRect.sizeDelta = new Vector2(1000f, 50f);
        subRect.anchoredPosition = titleRect.anchoredPosition + new Vector2(0f, -78f);

        Vector3 cubeScreen = cam.WorldToScreenPoint(from);
        Vector2 startPos = new Vector2(cubeScreen.x, cubeScreen.y) / scale;
        Vector2 centre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f) / scale;
        Vector2 centreUp = centre + new Vector2(0f, 20f);

        void SetOpen(float h)
        {
            mask.sizeDelta = new Vector2(PaperW, h);
            topRoller.rectTransform.anchoredPosition = new Vector2(0f, h * 0.5f + 12f);
            bottomRoller.rectTransform.anchoredPosition = new Vector2(0f, -h * 0.5f - 12f);
        }
        SetOpen(0f);
        group.localScale = Vector3.one * 0.12f;
        group.anchoredPosition = startPos;

        // ---- 1. the rolled scroll flies out of the broken bottle to the middle of the screen ----
        float t = 0f, fly = 0.8f;
        while (t < fly)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / fly));
            Vector2 p = Vector2.Lerp(startPos, centreUp, k);
            p.y += Mathf.Sin(k * Mathf.PI) * 90f;                       // a little hop
            group.anchoredPosition = p;
            group.localScale = Vector3.one * Mathf.Lerp(0.12f, 0.55f, k);
            group.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(k * Mathf.PI * 2f) * 14f * (1f - k) + (1f - k) * 25f);
            dim.color = new Color(0f, 0f, 0f, 0.4f * k);
            yield return null;
        }
        group.localRotation = Quaternion.identity;

        // ---- 2. it unrolls ----
        PixelAudio.Play("map_unroll");
        t = 0f;
        float open = 1.5f;
        while (t < open)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / open);
            float eased = 1f - Mathf.Pow(1f - k, 3f);
            SetOpen(PaperH * eased);
            group.localScale = Vector3.one * Mathf.Lerp(0.55f, 1f, eased);
            group.anchoredPosition = Vector2.Lerp(centreUp, centre, eased);
            // A little flutter of the paper as it opens.
            group.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 11f) * 1.2f * (1f - eased));
            yield return null;
        }
        group.localRotation = Quaternion.identity;
        SetOpen(PaperH);

        // ---- 3. read it ----
        PixelAudio.Play("map_reveal");
        t = 0f;
        float read = readSeconds > 0f ? readSeconds : 1.8f;
        while (t < read)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / 0.4f);
            title.color = new Color(1f, 0.92f, 0.6f, k);
            sub.color = new Color(1f, 1f, 1f, Mathf.Clamp01((t - 0.3f) / 0.4f));
            title.rectTransform.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(Mathf.Clamp01(t / 0.5f) * Mathf.PI));
            group.anchoredPosition = centre + new Vector2(0f, Mathf.Sin(t * 2.4f) * 4f);
            yield return null;
        }

        // ---- 4. it flies into the spot it marks ----
        Vector3 spotScreen = cam.WorldToScreenPoint(spot);
        Vector2 target = new Vector2(spotScreen.x, spotScreen.y) / scale;
        t = 0f;
        float shrink = 0.8f;
        Vector2 from2 = group.anchoredPosition;
        while (t < shrink)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / shrink));
            group.anchoredPosition = Vector2.Lerp(from2, target, k);
            group.localScale = Vector3.one * Mathf.Lerp(1f, 0.06f, k);
            group.localRotation = Quaternion.Euler(0f, 0f, k * 160f);
            groupAlpha.alpha = 1f - Mathf.Clamp01((k - 0.7f) / 0.3f);
            dim.color = new Color(0f, 0f, 0f, 0.4f * (1f - k));
            title.color = new Color(1f, 0.92f, 0.6f, 1f - k);
            sub.color = new Color(1f, 1f, 1f, 1f - k);
            yield return null;
        }
        Destroy(canvasObject);
        if (mapTex != null) Destroy(mapTex);

        // ---- 5. the X appears ----
        PlaceMark(spot);
        showing = false;
    }

    private static Image NewImage(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image i = go.GetComponent<Image>();
        i.color = color;
        i.raycastTarget = false;
        return i;
    }

    private static Image Roller(Transform parent, string name, float width)
    {
        Image roller = NewImage(parent, name, new Color(0.5f, 0.31f, 0.15f, 1f));
        PixelUIKit.StyleBox(roller, false);
        roller.rectTransform.anchorMin = roller.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        roller.rectTransform.sizeDelta = new Vector2(width, 30f);
        Image capL = NewImage(roller.transform, "Cap L", new Color(0.82f, 0.62f, 0.3f, 1f)), capR = NewImage(roller.transform, "Cap R", new Color(0.82f, 0.62f, 0.3f, 1f));
        foreach (Image cap in new[] { capL, capR })
        {
            PixelUIKit.StyleBox(cap, false);
            cap.rectTransform.anchorMin = cap.rectTransform.anchorMax = new Vector2(cap == capL ? 0f : 1f, 0.5f);
            cap.rectTransform.sizeDelta = new Vector2(26f, 44f);
            cap.rectTransform.anchoredPosition = new Vector2(cap == capL ? 6f : -6f, 0f);
        }
        return roller;
    }

    // ------------------------------------------------------------------
    // Where the X goes, and the X itself
    // ------------------------------------------------------------------

    private bool PickSpot(Camera cam, float size, out Vector3 spot)
    {
        spot = default;
        Vector3 c = clicker.CubeBasePosition;
        float near = (spotNearest > 0f ? spotNearest : 3f) * size, far = Mathf.Max(near + size, (spotFarthest > 0f ? spotFarthest : 7f) * size);
        Vector3 cubeVp = cam.WorldToViewportPoint(c);
        for (int attempt = 0; attempt < 40; attempt++)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            float r = Random.Range(near, far);
            Vector3 probe = c + new Vector3(dir.x, 0f, dir.y) * r;
            Vector3 ground;
            if (!PixelBait.FindGround(clicker, probe, size, out ground)) continue;
            Vector3 vp = cam.WorldToViewportPoint(ground);
            if (vp.z <= 0f || vp.x < 0.12f || vp.x > 0.88f || vp.y < 0.2f || vp.y > 0.76f) continue;
            if (Vector2.Distance(new Vector2(vp.x * cam.aspect, vp.y), new Vector2(cubeVp.x * cam.aspect, cubeVp.y)) < 0.16f) continue;   // not hiding behind the cube
            bool apart = true;
            foreach (Mark m in marks) if (Vector3.Distance(m.pos, ground) < size * 2.2f) { apart = false; break; }
            if (!apart) continue;
            spot = ground;
            return true;
        }
        return false;
    }

    private Material Mat(ref Material field, Color color)
    {
        if (field == null) field = clicker.CreateVisualMaterial(color, false);
        return field;
    }

    private void PlaceMark(Vector3 pos)
    {
        float size = clicker.PixelBaseSize;
        int limit = maxMarks > 0 ? maxMarks : 3;
        for (int i = 0; i < marks.Count && marks.FindAll(x => !x.dying).Count >= limit; i++)
            if (!marks[i].dying) Fade(marks[i]);

        Mark m = new Mark { pos = pos, total = digClicks > 0 ? digClicks : 6, born = Time.time, nextRing = Time.unscaledTime + 0.3f };
        m.left = m.total;
        m.root = new GameObject("Buried Treasure X");
        m.root.transform.position = pos;
        m.root.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        // A patch of disturbed earth under the X, a mound that grows as you dig.
        GameObject patch = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        patch.name = "Patch";
        Destroy(patch.GetComponent<Collider>());
        patch.transform.SetParent(m.root.transform, false);
        patch.transform.localScale = new Vector3(size * 1.5f, size * 0.01f, size * 1.5f);
        patch.transform.localPosition = Vector3.up * size * 0.012f;
        patch.GetComponent<Renderer>().sharedMaterial = Mat(ref dirtMat, new Color(0.3f, 0.2f, 0.12f, 1f));
        patch.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        GameObject mound = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        mound.name = "Mound";
        Destroy(mound.GetComponent<Collider>());
        mound.transform.SetParent(m.root.transform, false);
        mound.transform.localPosition = Vector3.up * size * 0.02f;
        mound.GetComponent<Renderer>().sharedMaterial = Mat(ref dirtMat, new Color(0.3f, 0.2f, 0.12f, 1f));
        m.mound = mound.transform;
        mound.SetActive(false);

        // The X: two red planks laid crosswise.
        GameObject x = new GameObject("X");
        x.transform.SetParent(m.root.transform, false);
        x.transform.localPosition = Vector3.up * size * 0.05f;
        m.xRoot = x.transform;
        for (int i = 0; i < 2; i++)
        {
            GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = "Bar " + i;
            Destroy(bar.GetComponent<Collider>());
            bar.transform.SetParent(x.transform, false);
            bar.transform.localRotation = Quaternion.Euler(0f, i == 0 ? 45f : -45f, 0f);
            bar.transform.localScale = new Vector3(size * 1.15f, size * 0.07f, size * 0.2f);
            bar.GetComponent<Renderer>().sharedMaterial = Mat(ref redMat, new Color(0.85f, 0.12f, 0.1f, 1f));
            bar.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        marks.Add(m);

        PixelAudio.Play("map_reveal");
        PixelFxRing.Spawn(pos + Vector3.up * size * 0.05f, new Color(1f, 0.3f, 0.2f, 0.9f), size * 0.3f, size * 2.6f, 0.9f, 2f);
        DirtFx(pos, size, 0.8f);
        PixelHints.Announce("An X marks the spot - click it to dig!");
    }

    private void GlassFx(Vector3 at, float size)
    {
        if (PixelFx.Material == null) return;
        Transform t = FxRoot(at);
        Burst(t, "Glass", new Vector2(0.8f, 1.5f), new Vector2(2f, 5.5f), new Vector2(0.05f, 0.14f), 2.8f, new Color(0.75f, 1f, 0.95f, 0.95f), new Color(0.25f, 0.7f, 0.55f, 0.9f), 50, 0.5f, size);
        Burst(t, "Sparkles", new Vector2(0.7f, 1.4f), new Vector2(0.6f, 2.4f), new Vector2(0.04f, 0.1f), -0.08f, Color.white, new Color(0.8f, 1f, 0.95f), 28, 0.6f, size, true);
        PixelFxRing.Spawn(at, new Color(0.7f, 1f, 0.95f, 0.8f), size * 0.5f, size * 3.2f, 0.5f, 2f);
    }

    // ------------------------------------------------------------------
    // The map picture (drawn in code)
    // ------------------------------------------------------------------

    private const int MapW = 640, MapH = 420;

    private static float Smooth(float t) => t * t * (3f - 2f * t);

    private static float Value(float x, float y, int seed)
    {
        int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
        float fx = Smooth(x - ix), fy = Smooth(y - iy);
        float a = PixelNoise.Hash(ix, iy, seed), b = PixelNoise.Hash(ix + 1, iy, seed), c = PixelNoise.Hash(ix, iy + 1, seed), d = PixelNoise.Hash(ix + 1, iy + 1, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    private static void Blend(Color[] px, int x, int y, Color c)
    {
        if (x < 0 || y < 0 || x >= MapW || y >= MapH || c.a <= 0f) return;
        int i = y * MapW + x;
        px[i] = Color.Lerp(px[i], new Color(c.r, c.g, c.b, 1f), c.a);
    }

    private static void Disc(Color[] px, float cx, float cy, float r, Color c)
    {
        for (int y = Mathf.FloorToInt(cy - r - 1); y <= Mathf.CeilToInt(cy + r + 1); y++)
            for (int x = Mathf.FloorToInt(cx - r - 1); x <= Mathf.CeilToInt(cx + r + 1); x++)
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                if (d <= r) Blend(px, x, y, new Color(c.r, c.g, c.b, c.a * Mathf.Clamp01(r - d + 0.5f)));
            }
    }

    private static void Line(Color[] px, Vector2 a, Vector2 b, float thickness, Color c)
    {
        float len = Vector2.Distance(a, b);
        int steps = Mathf.Max(1, Mathf.CeilToInt(len * 1.5f));
        for (int i = 0; i <= steps; i++) Disc(px, Mathf.Lerp(a.x, b.x, i / (float)steps), Mathf.Lerp(a.y, b.y, i / (float)steps), thickness * 0.5f, c);
    }

    private static Vector2 MapPoint(Vector2 vp) => new Vector2(Mathf.Lerp(70f, MapW - 70f, Mathf.Clamp01(vp.x)), Mathf.Lerp(60f, MapH - 60f, Mathf.Clamp01(vp.y)));

    private Texture2D MakeMapTexture(Vector2 cubeVp, Vector2 spotVp)
    {
        Color[] px = new Color[MapW * MapH];
        Vector2 cubeP = MapPoint(cubeVp), xP = MapPoint(spotVp);
        int seed = Random.Range(1, 9999);

        // Old parchment: warm paper, stains and scorched edges.
        for (int y = 0; y < MapH; y++)
            for (int x = 0; x < MapW; x++)
            {
                float n = Value(x / 70f, y / 70f, seed) * 0.5f + Value(x / 23f, y / 23f, seed + 1) * 0.3f + Value(x / 6f, y / 6f, seed + 2) * 0.2f;
                Color c = Color.Lerp(new Color(0.94f, 0.84f, 0.6f), new Color(0.78f, 0.6f, 0.34f), n);
                float edge = Mathf.Min(Mathf.Min(x, MapW - 1 - x), Mathf.Min(y, MapH - 1 - y));
                float burn = Mathf.Clamp01(1f - edge / (20f + Value(x / 14f, y / 14f, seed + 3) * 34f));
                c = Color.Lerp(c, new Color(0.3f, 0.17f, 0.07f), burn * burn);
                if (Value(x / 40f, y / 40f, seed + 4) > 0.78f) c *= 0.93f;   // a coffee stain
                px[y * MapW + x] = c;
            }

        // The sea: little wave marks scattered over the paper.
        Color ink = new Color(0.36f, 0.27f, 0.16f, 0.55f);
        for (int gy = 30; gy < MapH - 30; gy += 34)
            for (int gx = 30; gx < MapW - 30; gx += 46)
            {
                float jx = gx + (PixelNoise.Hash(gx, gy, seed) - 0.5f) * 24f, jy = gy + (PixelNoise.Hash(gy, gx, seed + 5) - 0.5f) * 18f;
                if (Vector2.Distance(new Vector2(jx, jy), xP) < 120f) continue;   // the island is drawn there
                Line(px, new Vector2(jx - 10f, jy), new Vector2(jx - 4f, jy + 4f), 2f, ink);
                Line(px, new Vector2(jx - 4f, jy + 4f), new Vector2(jx + 2f, jy), 2f, ink);
                Line(px, new Vector2(jx + 2f, jy), new Vector2(jx + 8f, jy + 4f), 2f, ink);
            }

        // The island round the X: sand, grass, a rocky coast line.
        Vector2 isle = xP + new Vector2(-18f, -6f);
        float baseR = 112f;
        for (int y = 0; y < MapH; y++)
            for (int x = 0; x < MapW; x++)
            {
                Vector2 d = new Vector2(x - isle.x, (y - isle.y) * 1.25f);
                float dist = d.magnitude;
                if (dist > baseR * 1.5f) continue;
                float a = Mathf.Atan2(d.y, d.x);
                float shore = baseR * (1f + 0.2f * Mathf.Sin(3f * a + seed) + 0.13f * Mathf.Sin(5f * a + seed * 0.7f) + 0.07f * Mathf.Sin(9f * a + seed * 0.3f));
                if (dist < shore)
                {
                    float inner = dist / shore;
                    Color land = inner > 0.8f ? new Color(0.9f, 0.8f, 0.52f) : Color.Lerp(new Color(0.55f, 0.66f, 0.3f), new Color(0.4f, 0.55f, 0.24f), Value(x / 18f, y / 18f, seed + 6));
                    px[y * MapW + x] = Color.Lerp(px[y * MapW + x], land, 0.92f);
                    if (inner > 0.93f) px[y * MapW + x] = Color.Lerp(px[y * MapW + x], new Color(0.35f, 0.22f, 0.1f), (inner - 0.93f) / 0.07f);   // outline
                }
                else if (dist < shore + 9f) px[y * MapW + x] = Color.Lerp(px[y * MapW + x], new Color(0.45f, 0.7f, 0.78f), 0.35f * (1f - (dist - shore) / 9f));   // shallow water
            }
        // Palm trees and a couple of hills.
        for (int i = 0; i < 4; i++)
        {
            float a = i * 1.7f + seed;
            Vector2 p = isle + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 56f * (0.5f + PixelNoise.Hash(i, 1, seed));
            if (Vector2.Distance(p, xP) < 34f) continue;
            Line(px, p, p + new Vector2(2f, 16f), 3f, new Color(0.4f, 0.26f, 0.12f, 1f));
            for (int leaf = 0; leaf < 5; leaf++)
            {
                float la = leaf * 1.26f;
                Line(px, p + new Vector2(2f, 16f), p + new Vector2(2f, 16f) + new Vector2(Mathf.Cos(la), Mathf.Sin(la)) * 11f, 3f, new Color(0.15f, 0.45f, 0.2f, 1f));
            }
        }
        for (int i = 0; i < 2; i++)
        {
            Vector2 p = isle + new Vector2(40f - i * 90f, 30f + i * 12f);
            if (Vector2.Distance(p, xP) < 40f) continue;
            Line(px, p + new Vector2(-14f, 0f), p + new Vector2(0f, 20f), 3f, new Color(0.35f, 0.25f, 0.15f, 0.9f));
            Line(px, p + new Vector2(0f, 20f), p + new Vector2(14f, 0f), 3f, new Color(0.35f, 0.25f, 0.15f, 0.9f));
        }

        // The dotted path from the cube to the X.
        Vector2 mid = (cubeP + xP) * 0.5f;
        Vector2 perp = new Vector2(-(xP.y - cubeP.y), xP.x - cubeP.x).normalized * Vector2.Distance(cubeP, xP) * 0.22f;
        Vector2 ctrl = mid + perp;
        int dots = Mathf.Max(6, Mathf.RoundToInt(Vector2.Distance(cubeP, xP) / 15f));
        for (int i = 1; i < dots; i++)
        {
            float k = i / (float)dots;
            Vector2 p = (1 - k) * (1 - k) * cubeP + 2 * (1 - k) * k * ctrl + k * k * xP;
            Disc(px, p.x, p.y, 3.4f, new Color(0.55f, 0.1f, 0.08f, 1f));
        }

        // The cube: you are here.
        Vector2 cp = cubeP;
        Disc(px, cp.x, cp.y, 17f, new Color(0.3f, 0.2f, 0.1f, 0.35f));
        Line(px, cp + new Vector2(-12f, 8f), cp + new Vector2(0f, 14f), 7f, new Color(0.95f, 0.95f, 0.98f, 1f));
        Line(px, cp + new Vector2(0f, 14f), cp + new Vector2(12f, 8f), 7f, new Color(0.9f, 0.9f, 0.95f, 1f));
        Line(px, cp + new Vector2(-9f, -2f), cp + new Vector2(-9f, 4f), 11f, new Color(0.62f, 0.7f, 0.85f, 1f));
        Line(px, cp + new Vector2(9f, -2f), cp + new Vector2(9f, 4f), 11f, new Color(0.45f, 0.52f, 0.7f, 1f));

        // A compass in the corner.
        Vector2 comp = new Vector2(MapW - 78f, 82f);
        Disc(px, comp.x, comp.y, 38f, new Color(0.35f, 0.22f, 0.1f, 0.25f));
        for (int i = 0; i < 4; i++)
        {
            float a = i * Mathf.PI * 0.5f;
            Vector2 tip = comp + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (i == 1 ? 40f : 30f);
            Line(px, comp, tip, 7f, i == 1 ? new Color(0.7f, 0.1f, 0.08f, 1f) : new Color(0.3f, 0.2f, 0.1f, 1f));
        }
        Disc(px, comp.x, comp.y, 5f, new Color(0.95f, 0.85f, 0.5f, 1f));

        // The big X.
        Color dark = new Color(0.25f, 0.03f, 0.02f, 1f), red = new Color(0.82f, 0.08f, 0.06f, 1f);
        for (int pass = 0; pass < 2; pass++)
        {
            float th = pass == 0 ? 17f : 11f;
            Color col = pass == 0 ? dark : red;
            Line(px, xP + new Vector2(-30f, -30f), xP + new Vector2(30f, 30f), th, col);
            Line(px, xP + new Vector2(-30f, 30f), xP + new Vector2(30f, -30f), th, col);
        }
        for (int i = 0; i < 40; i++)   // a dashed ring round it
        {
            float a = i / 40f * Mathf.PI * 2f;
            if (i % 2 == 0) Disc(px, xP.x + Mathf.Cos(a) * 46f, xP.y + Mathf.Sin(a) * 46f, 2.4f, new Color(0.55f, 0.1f, 0.08f, 1f));
        }

        Texture2D tex = new Texture2D(MapW, MapH, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Treasure Map" };
        tex.SetPixels(px);
        tex.Apply(false, false);
        return tex;
    }
}
