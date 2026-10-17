// ---- System notes ----
// PixelCollectionGallery.cs
//   The Collection Log's GALLERY: the camera button on an entry's page (PixelCollectionLog) calls PixelCollectionGallery.Enter(entryIndex): you walk, in first person, round a PITCH BLACK room where every
//   collectible floats and lights up its surroundings. Everything is built when you enter and DESTROYED when you leave (Esc / the Leave button): nothing waits in the background. The room is far away from the
//   game (origin (-4000, -9000, -4000)), is seen only through its own camera drawing to a screen-sized RenderTexture that a full-screen canvas (sorting 900) shows, so no part of it can show in the normal view.
//   * Room (settings on PixelCollectionLog, header Gallery): a roomSize (20) square floor drawn as the first person NEON WIREFRAME grid (Resources/PixelNeonGrid.shader: the lines glow only near the lit
//     points = you and each cube) and a boundsSize (10) square you cannot leave (a faint wire wall + floor outline marks it).
//   * Items: rows (Dragon Cubes, God Cubes, Prime Cubes, the Cubelette) of real models (PixelClicker.CreateDisplayPixel) floating, turning and bobbing, each with its own point light (only the nearest few lights
//     are on at once). A cube you have not found is a dark '???' block with no light. The cube you look at shows its name and text at the bottom.
//   * Darkness: the scene's directional lights are switched off while you are inside, the ambient light / reflections / fog are forced to black / off (restored on leaving), the camera clears to black.
//   * Controls: mouse looks, W A S D / arrows walk, Shift runs, Esc leaves (PixelWindows entry); the game's own action keys are ignored meanwhile (PixelKeys.Pressed / Held check Active) and clicks on the cube are blocked.
// ----------------------
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

public class PixelCollectionGallery : MonoBehaviour
{
    public struct Settings
    {
        public float roomSize, boundsSize, walkSpeed, runFactor, lookSensitivity, itemSize, itemHeight, lightRange, lightIntensity;
    }

    private static PixelCollectionGallery instance;
    private static Sprite cameraSprite;

    /// <summary>True while the gallery is open (the game's own action keys are ignored then).</summary>
    public static bool Active => instance != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; cameraSprite = null; }

    /// <summary>A little camera picture for the entry pages' button (drawn once in code).</summary>
    public static Sprite CameraSprite()
    {
        if (cameraSprite != null) return cameraSprite;
        const int w = 32, h = 24;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        Color clear = new Color(0f, 0f, 0f, 0f), body = new Color(0.2f, 0.2f, 0.24f, 1f), lens = new Color(0.55f, 0.75f, 0.95f, 1f), rim = new Color(0.85f, 0.88f, 0.95f, 1f);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Color c = clear;
                if (x >= 1 && x < w - 1 && y >= 2 && y < h - 5) c = body;                    // the body
                if (x >= 9 && x < 15 && y >= h - 5 && y < h - 1) c = body;                    // the viewfinder bump
                if (x >= 3 && x < 7 && y >= h - 8 && y < h - 6) c = rim;                      // a little flash
                float dx = x - 16f, dy = y - 11f, d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d < 7f) c = rim;                                                          // the lens
                if (d < 5f) c = lens;
                if (d < 2f) c = Color.white;
                tex.SetPixel(x, y, c);
            }
        tex.Apply();
        cameraSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 16f);
        return cameraSprite;
    }

    /// <summary>Builds the room and walks in, starting beside this entry (an index into the Collection Log's entries). Does nothing when it is open already.</summary>
    public static void Enter(PixelClicker clicker, int entry, Settings settings)
    {
        if (instance != null || clicker == null) return;
        GameObject go = new GameObject("Collection Gallery");
        instance = go.AddComponent<PixelCollectionGallery>();
        instance.Begin(clicker, entry, settings);
    }

    // ------------------------------------------------------------------

    private class Item
    {
        public Transform root;
        public Light light;
        public Vector3 basePos;
        public float phase, spin;
        public int entry;
        public bool known;
    }

    private static readonly Vector3 Origin = new Vector3(-4000f, -9000f, -4000f);

    private PixelClicker clicker;
    private Settings cfg;
    private int startEntry;
    private Camera cam;
    private RenderTexture rt;
    private GameObject canvasRoot, room;
    private RawImage view;
    private TMP_Text infoLabel, hintLabel;
    private Material gridMat, wireMat;
    private readonly List<Item> items = new List<Item>();
    private readonly Vector4[] points = new Vector4[64];
    private float yaw, pitch;
    private Vector3 pos;

    // saved scene state
    private UnityEngine.Rendering.AmbientMode oldAmbientMode;
    private Color oldAmbient;
    private float oldReflection;
    private bool oldFog, oldCursorVisible;
    private CursorLockMode oldLock;
    private readonly List<Light> switchedOff = new List<Light>();
    private float lightScanTimer;

    private static float V(float value, float fallback) => value > 0f ? value : fallback;

    private void Begin(PixelClicker owner, int entry, Settings settings)
    {
        clicker = owner; cfg = settings; startEntry = entry;
        cfg.roomSize = V(cfg.roomSize, 20f); cfg.boundsSize = V(cfg.boundsSize, 10f); cfg.walkSpeed = V(cfg.walkSpeed, 4f); cfg.runFactor = V(cfg.runFactor, 1.8f);
        cfg.lookSensitivity = V(cfg.lookSensitivity, 0.12f); cfg.itemSize = V(cfg.itemSize, 0.9f); cfg.itemHeight = V(cfg.itemHeight, 1.5f);
        cfg.lightRange = V(cfg.lightRange, 5f); cfg.lightIntensity = V(cfg.lightIntensity, 3f);

        // Darkness (restored on leaving).
        oldAmbientMode = RenderSettings.ambientMode; oldAmbient = RenderSettings.ambientLight; oldReflection = RenderSettings.reflectionIntensity; oldFog = RenderSettings.fog;
        ForceDark();
        SwitchOffSun();

        room = new GameObject("Gallery Room");
        room.transform.SetParent(transform, false);
        room.transform.position = Origin;
        BuildFloor();
        BuildWall();
        BuildItems();

        // The camera draws to a screen-sized picture; a full-screen canvas shows it over everything else.
        GameObject camGo = new GameObject("Gallery Camera");
        camGo.transform.SetParent(transform, false);
        cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.fieldOfView = 70f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 80f;
        cam.allowHDR = false; cam.allowMSAA = false;
        MakeTarget();

        BuildCanvas();

        // Start beside the entry (a step back from it, looking at it).
        Vector3 start = Vector3.zero;
        foreach (Item it in items) if (it.entry == startEntry) start = it.basePos;
        pos = ClampToBounds(new Vector3(start.x, 1.7f, start.z - 1.3f));
        yaw = 0f; pitch = 0f;

        PixelWindows.Register(this, 180, () => instance == this, Leave);
        PixelClicker.ExternalClickBlock = true;
        oldLock = Cursor.lockState; oldCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        ApplyCamera();
    }

    private void ForceDark()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = Color.black;
        RenderSettings.reflectionIntensity = 0f;
        RenderSettings.fog = false;
    }

    /// <summary>The scene's directional light(s) reach every object however far away: they are switched off while the gallery is open (and again if the game turns one back on).</summary>
    private void SwitchOffSun()
    {
        Light[] all = PixelFind.All<Light>();
        foreach (Light l in all)
            if (l != null && l.type == LightType.Directional && l.enabled && !l.transform.IsChildOf(transform)) { l.enabled = false; switchedOff.Add(l); }
    }

    private void MakeTarget()
    {
        if (rt != null) { rt.Release(); Destroy(rt); }
        rt = new RenderTexture(Mathf.Max(64, Screen.width), Mathf.Max(64, Screen.height), 24, RenderTextureFormat.ARGB32) { name = "Gallery View" };
        cam.targetTexture = rt;
        if (view != null) view.texture = rt;
    }

    // ------------------------------------------------------------------
    // The room
    // ------------------------------------------------------------------

    private void BuildFloor()
    {
        GameObject floor = new GameObject("Floor Grid");
        floor.transform.SetParent(room.transform, false);
        floor.transform.localPosition = new Vector3(0f, 0.01f, 0f);
        floor.transform.localScale = new Vector3(cfg.roomSize, 1f, cfg.roomSize);
        Mesh mesh = new Mesh { name = "Gallery Floor" };
        mesh.vertices = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f) };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(1f, 0.1f, 1f));
        floor.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer mr = floor.AddComponent<MeshRenderer>();
        Shader sh = Shader.Find("Pixel Clicker/Neon Grid");
        if (sh == null) sh = PixelShaders.SpriteDefault();
        gridMat = new Material(sh);
        gridMat.SetColor("_Color", new Color(0.3f, 0.9f, 1f, 1f));
        gridMat.SetFloat("_Spacing", 1f);
        gridMat.SetFloat("_Brightness", 1.3f);
        mr.sharedMaterial = gridMat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    /// <summary>A faint wire wall round the walkable square (vertical lines and three rings), so you can see where the room ends for you.</summary>
    private void BuildWall()
    {
        float half = cfg.boundsSize * 0.5f, height = 3f, w = 0.02f;
        List<Vector3> v = new List<Vector3>(); List<int> t = new List<int>(); List<Color> c = new List<Color>();
        Color col = new Color(0.3f, 0.9f, 1f, 0.35f);
        void Quad(Vector3 a, Vector3 b, Vector3 up, Vector3 side)
        {
            int i = v.Count;
            v.Add(a - side); v.Add(a + side); v.Add(b + side); v.Add(b - side);
            for (int k = 0; k < 4; k++) c.Add(col);
            t.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }
        Vector3[] corners = { new Vector3(-half, 0f, -half), new Vector3(-half, 0f, half), new Vector3(half, 0f, half), new Vector3(half, 0f, -half) };
        for (int s = 0; s < 4; s++)
        {
            Vector3 p0 = corners[s], p1 = corners[(s + 1) % 4], dir = (p1 - p0).normalized, side = dir * w;
            int n = Mathf.RoundToInt(cfg.boundsSize);
            for (int i = 0; i <= n; i++) { Vector3 p = Vector3.Lerp(p0, p1, i / (float)n); Quad(p, p + Vector3.up * height, Vector3.up, dir * w); }
            for (int r = 0; r <= 3; r++)
            {
                Vector3 up = Vector3.up * (r * height / 3f);
                Vector3 a = p0 + up, b = p1 + up;
                int i0 = v.Count; Vector3 sd = Vector3.up * w;
                v.Add(a - sd); v.Add(a + sd); v.Add(b + sd); v.Add(b - sd);
                for (int k = 0; k < 4; k++) c.Add(col);
                t.AddRange(new[] { i0, i0 + 1, i0 + 2, i0, i0 + 2, i0 + 3 });
            }
        }
        Mesh mesh = new Mesh { name = "Gallery Wall" };
        mesh.SetVertices(v); mesh.SetColors(c); mesh.SetTriangles(t, 0);
        mesh.RecalculateBounds();
        GameObject wall = new GameObject("Bounds Wire");
        wall.transform.SetParent(room.transform, false);
        wall.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer mr = wall.AddComponent<MeshRenderer>();
        wireMat = new Material(PixelShaders.SpriteDefault());
        mr.sharedMaterial = wireMat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    private void BuildItems()
    {
        int total = PixelCollectionLog.EntryCount;
        // Rows: Dragon Cubes (7), God Cubes (3), Prime Cubes (6), the Cubelette. Rows are 2.6 apart; a row is spread over the walkable width.
        float half = cfg.boundsSize * 0.5f;
        List<int>[] rows = { new List<int>(), new List<int>(), new List<int>(), new List<int>() };
        for (int i = 0; i < total; i++)
        {
            PixelClicker.PixelType type = PixelCollectionLog.EntryType(i);
            rows[PixelClicker.IsDragonCube(type) ? 0 : PixelClicker.IsInfinityStone(type) ? 2 : type == PixelClicker.PixelType.InfinityCubelette ? 3 : 1].Add(i);
        }
        float[] rowZ = { half - 1.2f, half - 3.8f, half - 6.4f, half - 9f };
        for (int r = 0; r < rows.Length; r++)
        {
            int count = rows[r].Count;
            float step = Mathf.Min(1.5f, (cfg.boundsSize - 1.4f) / Mathf.Max(1, count - 1));
            for (int k = 0; k < count; k++)
            {
                int entry = rows[r][k];
                Vector3 local = new Vector3((k - (count - 1) * 0.5f) * step, cfg.itemHeight, rowZ[r]);
                items.Add(MakeItem(entry, local));
            }
        }
    }

    private Item MakeItem(int entry, Vector3 local)
    {
        Item it = new Item { entry = entry, basePos = local, phase = Random.value * 6.28f, spin = Random.Range(25f, 45f) };
        GameObject holder = new GameObject("Item " + entry);
        holder.transform.SetParent(room.transform, false);
        holder.transform.localPosition = local;
        it.root = holder.transform;
        it.known = PixelCollectionLog.EntryKnown(clicker, entry, out int tier);
        if (it.known && tier >= 0)
        {
            GameObject model = clicker.CreateDisplayPixel(tier, holder.transform, 1f);
            if (model != null)
            {
                foreach (Collider col in model.GetComponentsInChildren<Collider>()) Destroy(col);
                model.transform.localPosition = Vector3.zero;
                model.transform.localScale *= cfg.itemSize;
            }
            GameObject lg = new GameObject("Light");
            lg.transform.SetParent(holder.transform, false);
            it.light = lg.AddComponent<Light>();
            it.light.type = LightType.Point;
            it.light.color = Color.Lerp(clicker.Tiers[tier].UIColor, Color.white, 0.35f);
            it.light.range = cfg.lightRange;
            it.light.intensity = cfg.lightIntensity;
            it.light.shadows = LightShadows.None;
        }
        else
        {
            // Not found yet: a dark block that only shows when another cube's light falls on it.
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(block.GetComponent<Collider>());
            block.transform.SetParent(holder.transform, false);
            block.transform.localScale = Vector3.one * cfg.itemSize;
            Material m = new Material(clicker.CreateVisualMaterial(new Color(0.04f, 0.04f, 0.05f, 1f), false));
            block.GetComponent<Renderer>().sharedMaterial = m;
        }
        return it;
    }

    // ------------------------------------------------------------------
    // The screen
    // ------------------------------------------------------------------

    private void BuildCanvas()
    {
        TMP_FontAsset font = clicker.UIFont;
        canvasRoot = PixelUIKit.CreateCanvas("Collection Gallery View", 900, new Vector2(1920f, 1080f), true);
        GameObject img = new GameObject("View", typeof(RectTransform), typeof(RawImage));
        img.transform.SetParent(canvasRoot.transform, false);
        view = img.GetComponent<RawImage>();
        view.texture = rt;
        view.color = Color.white;
        PixelUIKit.Stretch(view.rectTransform);

        GameObject dot = new GameObject("Crosshair", typeof(RectTransform), typeof(Image));
        dot.transform.SetParent(canvasRoot.transform, false);
        dot.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.6f);
        dot.GetComponent<Image>().raycastTarget = false;
        dot.GetComponent<RectTransform>().sizeDelta = new Vector2(6f, 6f);

        hintLabel = PixelUIKit.CreateText(font, canvasRoot.transform, "Hint", "WASD / arrows walk   -   mouse looks   -   Shift runs   -   Esc leaves", 26f, TextAlignmentOptions.Center, FontStyles.Normal, new Color(1f, 1f, 1f, 0.55f));
        RectTransform hr = hintLabel.rectTransform;
        hr.anchorMin = new Vector2(0f, 1f); hr.anchorMax = new Vector2(1f, 1f); hr.pivot = new Vector2(0.5f, 1f);
        hr.sizeDelta = new Vector2(0f, 44f); hr.anchoredPosition = new Vector2(0f, -24f);

        infoLabel = PixelUIKit.CreateText(font, canvasRoot.transform, "Info", "", 32f, TextAlignmentOptions.Center, FontStyles.Bold, Color.white);
        RectTransform ir = infoLabel.rectTransform;
        ir.anchorMin = new Vector2(0.1f, 0f); ir.anchorMax = new Vector2(0.9f, 0f); ir.pivot = new Vector2(0.5f, 0f);
        ir.sizeDelta = new Vector2(0f, 120f); ir.anchoredPosition = new Vector2(0f, 40f);
    }

    // ------------------------------------------------------------------
    // Frame
    // ------------------------------------------------------------------

    private Vector3 ClampToBounds(Vector3 p)
    {
        float h = cfg.boundsSize * 0.5f - 0.15f;
        return new Vector3(Mathf.Clamp(p.x, -h, h), p.y, Mathf.Clamp(p.z, -h, h));
    }

    private static bool Held(KeyCode a, KeyCode b) => PixelKeys.KeyHeld(a) || PixelKeys.KeyHeld(b);

    private void Update()
    {
        // Look.
        Vector2 delta;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        delta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
#else
        delta = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
#endif
        yaw += delta.x * cfg.lookSensitivity;
        pitch = Mathf.Clamp(pitch - delta.y * cfg.lookSensitivity, -85f, 85f);

        // Walk.
        float forward = (Held(KeyCode.W, KeyCode.UpArrow) ? 1f : 0f) - (Held(KeyCode.S, KeyCode.DownArrow) ? 1f : 0f);
        float right = (Held(KeyCode.D, KeyCode.RightArrow) ? 1f : 0f) - (Held(KeyCode.A, KeyCode.LeftArrow) ? 1f : 0f);
        Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * new Vector3(right, 0f, forward);
        if (dir.sqrMagnitude > 1f) dir.Normalize();
        float speed = cfg.walkSpeed * (Held(KeyCode.LeftShift, KeyCode.RightShift) ? cfg.runFactor : 1f);
        pos = ClampToBounds(pos + dir * speed * Time.unscaledDeltaTime);
        ApplyCamera();

        if (rt != null && (rt.width != Screen.width || rt.height != Screen.height) && Screen.width > 64 && Screen.height > 64) MakeTarget();

        // The cubes drift: a slow turn and a gentle bob.
        float t = Time.unscaledTime;
        Vector3 eye = cam.transform.position;
        foreach (Item it in items)
        {
            it.root.localRotation = Quaternion.Euler(0f, t * it.spin + it.phase * 57f, 0f);
            it.root.localPosition = it.basePos + Vector3.up * (Mathf.Sin(t * 0.9f + it.phase) * 0.12f);
        }

        UpdateLights(eye);
        UpdateInfo();
    }

    private void ApplyCamera()
    {
        cam.transform.SetPositionAndRotation(Origin + pos, Quaternion.Euler(pitch, yaw, 0f));
    }

    private readonly List<Item> byDistance = new List<Item>();

    /// <summary>Only the nearest few cube lights are on (the pipeline only shades so many at once, and they would pop in and out otherwise); the floor grid glows round you and every cube.</summary>
    private void UpdateLights(Vector3 eye)
    {
        byDistance.Clear();
        byDistance.AddRange(items);
        byDistance.Sort((a, b) => (a.root.position - eye).sqrMagnitude.CompareTo((b.root.position - eye).sqrMagnitude));
        int lit = 0;
        foreach (Item it in byDistance)
        {
            bool on = it.light != null && lit < 6;
            if (it.light != null) { it.light.enabled = on; if (on) lit++; }
        }
        int n = 0;
        points[n++] = new Vector4(pos.x, pos.z, 4.5f, 0f);
        foreach (Item it in items)
            if (it.light != null && n < points.Length) points[n++] = new Vector4(it.basePos.x, it.basePos.z, cfg.lightRange * 0.8f, 0f);
        // The grid's shader works in world space, so the points are given in world space too.
        for (int i = 0; i < n; i++) { points[i].x += Origin.x; points[i].y += Origin.z; }
        gridMat.SetFloat("_Count", n);
        gridMat.SetVectorArray("_Points", points);
    }

    private void UpdateInfo()
    {
        Item best = null; float bestAngle = 9f;
        Vector3 eye = cam.transform.position, fwd = cam.transform.forward;
        foreach (Item it in items)
        {
            Vector3 to = it.root.position - eye;
            float d = to.magnitude;
            if (d > 7f) continue;
            float a = Vector3.Angle(fwd, to);
            if (a < bestAngle) { bestAngle = a; best = it; }
        }
        if (best == null) { infoLabel.text = ""; return; }
        string name = best.known ? PixelCollectionLog.EntryName(clicker, best.entry) : "???";
        string text = best.known ? PixelCollectionLog.EntryFlavour(best.entry) : "Not found yet";
        infoLabel.text = name + "\n<size=70%><b>" + text + "</b></size>";
    }

    private void LateUpdate()
    {
        ForceDark();   // (the day / night cycle and the sky style write these every frame)
        lightScanTimer -= Time.unscaledDeltaTime;
        if (lightScanTimer <= 0f) { lightScanTimer = 0.5f; SwitchOffSun(); }
    }

    // ------------------------------------------------------------------
    // Leaving
    // ------------------------------------------------------------------

    private void Leave()
    {
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (instance != this) return;
        instance = null;
        PixelWindows.Unregister(this);
        PixelClicker.ExternalClickBlock = false;
        Cursor.lockState = oldLock; Cursor.visible = oldCursorVisible;
        RenderSettings.ambientMode = oldAmbientMode; RenderSettings.ambientLight = oldAmbient; RenderSettings.reflectionIntensity = oldReflection; RenderSettings.fog = oldFog;
        foreach (Light l in switchedOff) if (l != null) l.enabled = true;
        if (cam != null) cam.targetTexture = null;
        if (rt != null) { rt.Release(); Destroy(rt); }
        if (canvasRoot != null) Destroy(canvasRoot);
        if (gridMat != null) Destroy(gridMat);
        if (wireMat != null) Destroy(wireMat);
        PixelCollectionLog.ShowEntry(startEntry);   // back to the book, on the same page
    }
}
