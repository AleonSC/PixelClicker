// ---- System notes (moved here from CLAUDE.md) ----
// PixelDevShapes.cs / PixelDevMockup.cs
//   Layout dev tools ('Mock up mode' tick box in the Dev Tools Tools tab = PixelDevMockWindow, ONE draggable window with a Shapes tab and a UI pieces tab
//   and a minimize button; both components are created by PixelDevTools at start (Boot) so saved layouts reappear, the host window turns the tabs on with
//   SetActive; their controls are built into PixelDevMockWindow.TabRoot; state in PlayerPrefs
//   PixelClicker.Dev.*, not in the game save). Shape placer (tab 0); click the floor to drop the brush shape (ShapeKind Cube /
//   Sphere / Cylinder / Capsule / Ramp (runtime wedge mesh) / Wall / Disc, sizes in main-pixel widths), click a shape to select + drag
//   it on the floor plane, Q / E (PixelAction.RotateLeft/Right) turn, the wheel turns smoothly and Ctrl + wheel resizes (the hovered / dragged shape, else the brush), a see-through GHOST of the brush follows the mouse over the floor (UpdateGhost) and the shape is dropped with the brush's size / turn, Delete removes; ShapeMode Solid (static collider)
//   / Dynamic (rigidbody) / Ghost (no collider); colour cycle button; every shape has a billboard number label, the panel lists id /
//   kind / mode / x z y / rotation / size and 'Copy layout' puts the text on the clipboard; 'RESET ALL SHAPES' deletes everything (one
//   click). While open PixelClicker.DevClickBlock (part of ExternalClickBlock) keeps the cube from taking the clicks. UI mockup
//   builder (tab 1); right-click on a piece deletes it; resize grips are a drawn corner grip (PixelUIKit.GripSprite, also on My
//   Window); add Window / Box / Button / Text / Image pieces (own canvas 540, 1920 x 1080 units, origin top-left, no UI-scale
//   division) - drag to move, yellow corner handle to resize, number badge, exact X / Y / W / H fields, text box, font size, opacity
//   slider, hex colour, layer Front / Up / Down / Back, duplicate, delete; Image pieces show PNG / JPG files from
//   <persistentDataPath>/Mockups (Folder button opens it, Refresh rescans; a checker placeholder otherwise); 'Show when closed' keeps
//   the pieces visible after the panel is closed, 'Hide' hides them; 'Copy layout' lists every piece back to front; 'RESET ALL'
//   deletes all.
// ---------------------------------------------------
using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static PixelInput;

/// <summary>
/// Dev tool: a SHAPE PLACER. Click the floor to drop a shape (cube, sphere, cylinder, capsule, ramp, wall, disc), click a shape to select and
/// drag it, the wheel rotates it smoothly, Ctrl + wheel scales it, Delete removes it. Every shape carries a number label so a screenshot can say "move #3
/// next to #5"; the panel lists each shape's exact position / rotation / size and can copy the whole layout as text. Shapes are SOLID (a static
/// collider), DYNAMIC (a rigidbody that falls and gets pushed) or GHOST (no collider). The layout is saved (PlayerPrefs, dev settings are not
/// part of the game save) and restored on the next start; <b>Reset all</b> deletes every shape. Opened from the Dev Tools tick box.
/// </summary>
public class PixelDevShapes : MonoBehaviour
{
    public enum ShapeKind { Cube, Sphere, Cylinder, Capsule, Ramp, Wall, Disc }
    public enum ShapeMode { Solid, Dynamic, Ghost }

    private const string PrefData = "PixelClicker.Dev.ShapesData";

    private static PixelDevShapes instance;

    /// <summary>The shape placer panel is open.</summary>
    public static bool Active { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; Active = false; }

    /// <summary>Makes sure the placer exists (so saved shapes appear) and opens / closes its panel.</summary>
    public static void SetActive(bool on)
    {
        Boot();
        instance.Apply(on);
    }

    /// <summary>Creates the placer (loading the saved shapes) if there is something saved.</summary>
    public static void Boot()
    {
        if (instance != null) return;
        GameObject go = new GameObject("Dev Shapes");
        instance = go.AddComponent<PixelDevShapes>();
    }

    [Serializable]
    private class ShapeData
    {
        public int id;
        public int kind;
        public Vector3 pos;
        public float yaw;
        public float size = 1f;
        public int mode;
        public int color;
    }

    [Serializable]
    private class SaveBlob
    {
        public int nextId = 1;
        public List<ShapeData> shapes = new List<ShapeData>();
    }

    private class Shape
    {
        public ShapeData data;
        public GameObject go;
        public MeshFilter filter;
        public Renderer render;
        public TextMeshPro label;
    }

    private static readonly Color[] Palette =
    {
        new Color(0.85f, 0.85f, 0.88f), new Color(0.9f, 0.3f, 0.3f), new Color(0.95f, 0.6f, 0.2f), new Color(0.95f, 0.85f, 0.25f),
        new Color(0.35f, 0.8f, 0.4f), new Color(0.3f, 0.7f, 0.95f), new Color(0.6f, 0.4f, 0.9f), new Color(0.25f, 0.25f, 0.3f),
    };
    private static readonly string[] ColorNames = { "White", "Red", "Orange", "Yellow", "Green", "Blue", "Purple", "Dark" };

    private readonly List<Shape> shapes = new List<Shape>();
    private SaveBlob blob = new SaveBlob();
    private Shape selected;
    private bool dragging;
    private Plane dragPlane;
    private Vector3 dragOffset;
    private bool dirty;
    private float saveTimer;

    private PixelClicker clicker;
    private Camera cam;
    private TMP_FontAsset font;

    // Panel
    private TMP_Dropdown kindDropdown, modeDropdown;
    private int[] kindMap, modeMap;   // option -> enum value (the lists are alphabetical)
    private int PickedKind => kindDropdown != null ? PixelUIKit.Original(kindMap, kindDropdown.value) : 0;
    private int PickedMode => modeDropdown != null ? PixelUIKit.Original(modeMap, modeDropdown.value) : 0;
    private TMP_Text readout, colourLabel;
    private bool buildingUi;

    private static readonly Dictionary<ShapeKind, Mesh> meshCache = new Dictionary<ShapeKind, Mesh>();

    private void Awake()
    {
        instance = this;
        clicker = PixelFind.First<PixelClicker>();
        font = clicker != null ? clicker.UIFont : null;
        PixelDevMockWindow.Boot();
        Load();
        BuildPanel();
        Apply(false);
    }

    private void OnDestroy()
    {
        if (instance == this) { instance = null; Active = false; }
        PixelClicker.DevClickBlock = false;
        if (ghost != null) Destroy(ghost);
        if (ghostMat != null) Destroy(ghostMat);
    }

    private void Apply(bool on)
    {
        Active = on;
        foreach (Shape s in shapes) if (s.label != null) s.label.gameObject.SetActive(on);
        if (!on) { dragging = false; PixelClicker.DevClickBlock = false; if (ghost != null) ghost.SetActive(false); }
        Refresh();
    }

    // ------------------------------------------------------------------
    // Shapes
    // ------------------------------------------------------------------

    private float Unit => clicker != null ? Mathf.Max(0.05f, clicker.PixelBaseSize) : 1f;

    private static Mesh MeshOf(ShapeKind kind)
    {
        if (meshCache.TryGetValue(kind, out Mesh cached) && cached != null) return cached;
        Mesh mesh;
        switch (kind)
        {
            case ShapeKind.Sphere: mesh = PrimitiveMesh(PrimitiveType.Sphere); break;
            case ShapeKind.Cylinder:
            case ShapeKind.Disc: mesh = PrimitiveMesh(PrimitiveType.Cylinder); break;
            case ShapeKind.Capsule: mesh = PrimitiveMesh(PrimitiveType.Capsule); break;
            case ShapeKind.Ramp: mesh = RampMesh(); break;
            default: mesh = PrimitiveMesh(PrimitiveType.Cube); break;
        }
        meshCache[kind] = mesh;
        return mesh;
    }

    private static Mesh PrimitiveMesh(PrimitiveType type)
    {
        GameObject temp = GameObject.CreatePrimitive(type);
        Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
        Destroy(temp);
        return mesh;
    }

    /// <summary>A wedge: 1 wide, 1 long, 1 high at the back, sloping down to nothing at the front (+Z).</summary>
    private static Mesh RampMesh()
    {
        Vector3 a = new Vector3(-0.5f, -0.5f, -0.5f), b = new Vector3(0.5f, -0.5f, -0.5f), c = new Vector3(0.5f, 0.5f, -0.5f), d = new Vector3(-0.5f, 0.5f, -0.5f);
        Vector3 e = new Vector3(-0.5f, -0.5f, 0.5f), f = new Vector3(0.5f, -0.5f, 0.5f);
        List<Vector3> v = new List<Vector3>();
        List<int> t = new List<int>();
        void Tri(Vector3 p, Vector3 q, Vector3 r)
        {
            // Unity draws the side whose normal is cross(q - p, r - p): make that face point away from a point inside the wedge.
            if (Vector3.Dot(Vector3.Cross(q - p, r - p), (p + q + r) / 3f - new Vector3(0f, -0.3f, -0.2f)) < 0f) { Vector3 swap = q; q = r; r = swap; }
            int i = v.Count;
            v.Add(p); v.Add(q); v.Add(r);
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
        }
        void Quad(Vector3 p, Vector3 q, Vector3 r, Vector3 s) { Tri(p, q, r); Tri(p, r, s); }
        Quad(a, d, c, b);      // back wall
        Quad(e, f, b, a);      // bottom (seen from below)
        Quad(d, e, f, c);      // the slope: from the top of the back down to the front edge
        Tri(a, e, d);          // left side
        Tri(b, c, f);          // right side
        Mesh mesh = new Mesh { name = "Dev Ramp" };
        mesh.SetVertices(v);
        mesh.SetTriangles(t, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private Vector3 BaseScale(ShapeKind kind)
    {
        switch (kind)
        {
            case ShapeKind.Cylinder: return new Vector3(1f, 0.5f, 1f);     // (the primitive cylinder is 2 high)
            case ShapeKind.Capsule: return new Vector3(1f, 0.75f, 1f);
            case ShapeKind.Wall: return new Vector3(3f, 1.5f, 0.25f);
            case ShapeKind.Disc: return new Vector3(2f, 0.06f, 2f);
            default: return Vector3.one;
        }
    }

    private Shape CreateShape(ShapeData data)
    {
        GameObject go = new GameObject("Dev Shape " + data.id);
        go.transform.SetParent(transform, false);
        MeshFilter filter = go.AddComponent<MeshFilter>();
        MeshRenderer render = go.AddComponent<MeshRenderer>();
        GameObject sample = GameObject.CreatePrimitive(PrimitiveType.Cube);
        render.sharedMaterial = new Material(sample.GetComponent<MeshRenderer>().sharedMaterial);
        Destroy(sample);

        GameObject labelGo = new GameObject("Label");
        labelGo.transform.SetParent(go.transform, false);
        TextMeshPro label = labelGo.AddComponent<TextMeshPro>();
        if (font != null) label.font = font;
        label.alignment = TextAlignmentOptions.Center;
        label.fontStyle = FontStyles.Bold;
        label.color = Color.white;
        label.outlineWidth = 0.25f;
        label.outlineColor = Color.black;
        label.raycastTarget = false;

        Shape shape = new Shape { data = data, go = go, filter = filter, render = render, label = label };
        shapes.Add(shape);
        Rebuild(shape);
        return shape;
    }

    /// <summary>Applies the data to the object: mesh, size, colour, collider / rigidbody for the mode.</summary>
    private void Rebuild(Shape s)
    {
        ShapeKind kind = (ShapeKind)Mathf.Clamp(s.data.kind, 0, Enum.GetValues(typeof(ShapeKind)).Length - 1);
        s.filter.sharedMesh = MeshOf(kind);
        s.go.transform.position = s.data.pos;
        s.go.transform.rotation = Quaternion.Euler(0f, s.data.yaw, 0f);
        s.go.transform.localScale = BaseScale(kind) * (Unit * Mathf.Max(0.05f, s.data.size));
        Color color = Palette[Mathf.Abs(s.data.color) % Palette.Length];
        s.render.material.color = color;

        Rigidbody old = s.go.GetComponent<Rigidbody>();
        if (old != null) DestroyImmediate(old);
        foreach (Collider c in s.go.GetComponents<Collider>()) DestroyImmediate(c);

        ShapeMode mode = (ShapeMode)Mathf.Clamp(s.data.mode, 0, 2);
        if (mode != ShapeMode.Ghost)
        {
            if (kind == ShapeKind.Cube || kind == ShapeKind.Wall) s.go.AddComponent<BoxCollider>();
            else if (kind == ShapeKind.Sphere) s.go.AddComponent<SphereCollider>();
            else
            {
                MeshCollider mc = s.go.AddComponent<MeshCollider>();
                mc.convex = true;
                mc.sharedMesh = s.filter.sharedMesh;
            }
        }
        if (mode == ShapeMode.Dynamic)
        {
            Rigidbody rb = s.go.AddComponent<Rigidbody>();
            rb.mass = 2f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        s.label.text = s.data.id.ToString();
        s.label.fontSize = Mathf.Max(2f, Unit * 14f);
        s.label.gameObject.SetActive(Active);
        PositionLabel(s);
    }

    private void PositionLabel(Shape s)
    {
        if (s.label == null) return;
        Bounds b = s.render.bounds;
        s.label.transform.position = new Vector3(b.center.x, b.max.y + Unit * 0.4f, b.center.z);
        if (cam != null) s.label.transform.rotation = cam.transform.rotation;
    }

    private void AddShape(Vector3 floorPoint)
    {
        ShapeData data = new ShapeData
        {
            id = blob.nextId++,
            kind = PickedKind,
            mode = PickedMode,
            color = brushColor,
            size = brushSize,
            yaw = brushYaw,
            pos = floorPoint,
        };
        blob.shapes.Add(data);
        Shape s = CreateShape(data);
        // Sit it on the floor: lift by half its height.
        data.pos = floorPoint + Vector3.up * s.render.bounds.extents.y;
        s.go.transform.position = data.pos;
        PositionLabel(s);
        Select(s);
        dirty = true;
    }

    private int brushColor;
    private float brushSize = 1f;
    private float brushYaw, brushYawTarget;   // the brush's turn (the ghost and the next shape placed)
    private float selYawTarget;               // where the selected shape is turning to (smooth wheel / key turning)
    private GameObject ghost;
    private MeshFilter ghostFilter;
    private Material ghostMat;

    private void Select(Shape s)
    {
        selected = s;
        if (s != null) selYawTarget = s.data.yaw;
        if (s != null && kindDropdown != null)
        {
            buildingUi = true;
            kindDropdown.SetValueWithoutNotify(PixelUIKit.OptionOf(kindMap, s.data.kind));
            modeDropdown.SetValueWithoutNotify(PixelUIKit.OptionOf(modeMap, s.data.mode));
            kindDropdown.RefreshShownValue();
            modeDropdown.RefreshShownValue();
            brushColor = s.data.color;
            brushSize = s.data.size;
            buildingUi = false;
        }
        foreach (Shape other in shapes) if (other.label != null) other.label.color = other == selected ? new Color(1f, 0.9f, 0.2f) : Color.white;
        Refresh();
    }

    private void DeleteSelected()
    {
        if (selected == null) return;
        blob.shapes.Remove(selected.data);
        shapes.Remove(selected);
        Destroy(selected.go);
        selected = null;
        dirty = true;
        Refresh();
    }

    private void ResetAll()
    {
        foreach (Shape s in shapes) if (s.go != null) Destroy(s.go);
        shapes.Clear();
        blob = new SaveBlob();
        selected = null;
        dragging = false;
        dirty = true;
        SaveNow();
        Refresh();
    }

    // ------------------------------------------------------------------
    // Save / load
    // ------------------------------------------------------------------

    private void Load()
    {
        string json = PixelPrefs.GetString(PrefData, "");
        if (string.IsNullOrEmpty(json)) return;
        try { blob = JsonUtility.FromJson<SaveBlob>(json) ?? new SaveBlob(); }
        catch (Exception) { blob = new SaveBlob(); }
        if (blob.shapes == null) blob.shapes = new List<ShapeData>();
        foreach (ShapeData d in blob.shapes) { d.id = Mathf.Max(1, d.id); blob.nextId = Mathf.Max(blob.nextId, d.id + 1); CreateShape(d); }
    }

    private void SaveNow()
    {
        dirty = false;
        PixelPrefs.SetString(PrefData, JsonUtility.ToJson(blob));
        PixelPrefs.Save();
    }

    // ------------------------------------------------------------------
    // Input
    // ------------------------------------------------------------------

    private Shape PickShape()
    {
        if (cam == null) return null;
        Vector2 mouse = PointerPosition();
        Shape best = null;
        float bestDistance = float.MaxValue;
        foreach (Shape s in shapes)
        {
            Vector3 center = cam.WorldToScreenPoint(s.render.bounds.center);
            if (center.z <= 0f) continue;
            Vector3 edge = cam.WorldToScreenPoint(s.render.bounds.center + cam.transform.right * s.render.bounds.extents.magnitude);
            float radius = Mathf.Max(30f, Mathf.Abs(edge.x - center.x) * 0.8f);
            float distance = Vector2.Distance(mouse, new Vector2(center.x, center.y));
            if (distance < radius && distance < bestDistance) { best = s; bestDistance = distance; }
        }
        return best;
    }

    private bool FloorPoint(Ray ray, out Vector3 point)
    {
        RaycastHit[] hits = Physics.RaycastAll(ray, 500f, ~0, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (p, q) => p.distance.CompareTo(q.distance));
        foreach (RaycastHit hit in hits)
        {
            if (hit.rigidbody != null) continue;   // the cube, old pixels, dynamic shapes
            point = hit.point;
            return true;
        }
        Plane ground = new Plane(Vector3.up, new Vector3(0f, clicker != null ? clicker.transform.position.y - Unit * 0.5f : 0f, 0f));
        if (ground.Raycast(ray, out float enter)) { point = ray.GetPoint(enter); return true; }
        point = Vector3.zero;
        return false;
    }

    private void Update()
    {
        if (!Active) return;
        if (cam == null) cam = PixelClicker.ViewCameraOf(clicker);
        if (cam == null) return;

        bool free = !PixelKeys.Typing && Time.timeScale > 0f && !PixelCalm.InMenu;
        bool overUi = PointerOverUI();
        PixelClicker.DevClickBlock = free;   // the cube ignores clicks while the placer is open
        Shape hoverShape = free && !overUi ? PickShape() : null;

        if (free && !overUi) HandleShapeMouse(hoverShape);
        if (!LeftHeld()) dragging = false;

        if (free) TurnShapes(hoverShape);
        UpdateGhost(free && !overUi && !dragging && hoverShape == null);

        if (free && selected != null)
        {
            if (PixelKeys.KeyPressed(KeyCode.Delete) || PixelKeys.KeyPressed(KeyCode.Backspace)) DeleteSelected();
        }

        foreach (Shape s in shapes) PositionLabel(s);
        saveTimer -= Time.unscaledDeltaTime;
        if (dirty && saveTimer <= 0f && !dragging) { SaveNow(); saveTimer = 0.5f; Refresh(); }
    }

    // Update step (mouse free and not over the UI): click to place / select and drag, right-click to delete, wheel turns / Ctrl + wheel resizes.
    private void HandleShapeMouse(Shape hoverShape)
    {
        Ray ray = cam.ScreenPointToRay(PointerPosition());
        if (LeftPressed())
        {
            Shape hit = PickShape();
            if (hit != null)
            {
                Select(hit);
                dragging = true;
                dragPlane = new Plane(Vector3.up, hit.go.transform.position);
                dragOffset = dragPlane.Raycast(ray, out float enter) ? hit.go.transform.position - ray.GetPoint(enter) : Vector3.zero;
            }
            else if (FloorPoint(ray, out Vector3 point)) AddShape(point);
        }
        if (RightPressed())
        {
            Shape doomed = PickShape();   // right-click removes the shape under the mouse
            if (doomed != null) { Select(doomed); DeleteSelected(); dragging = false; }
        }
        if (dragging && selected != null && LeftHeld())
        {
            if (dragPlane.Raycast(ray, out float enter))
            {
                selected.data.pos = ray.GetPoint(enter) + dragOffset;
                Rebuild(selected);
                dirty = true;
            }
        }
        // Wheel: turns smoothly; Ctrl + wheel resizes. It acts on the shape under the mouse (or the one being dragged), else on the brush (the ghost).
        float scroll = ScrollY();
        Shape target = dragging ? selected : hoverShape;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            if (target != null && target != selected) Select(target);
            if (ControlHeld())
            {
                float factor = Mathf.Pow(1.1f, scroll);
                if (target != null)
                {
                    target.data.size = Mathf.Clamp(target.data.size * factor, 0.1f, 40f);
                    brushSize = target.data.size;
                    Rebuild(target);
                    dirty = true;
                }
                else brushSize = Mathf.Clamp(brushSize * factor, 0.1f, 40f);
            }
            else if (target != null) selYawTarget += scroll * 15f;
            else brushYawTarget += scroll * 15f;
        }
    }

    // Update step: Q / E turn too; the turning itself is smoothed towards its target.
    private void TurnShapes(Shape hoverShape)
    {
        float turn = (PixelKeys.Held(PixelAction.RotateRight) ? 1f : 0f) - (PixelKeys.Held(PixelAction.RotateLeft) ? 1f : 0f);
        bool onShape = selected != null && (dragging || hoverShape == selected);
        if (Mathf.Abs(turn) > 0.1f)
        {
            if (onShape) selYawTarget += turn * 120f * Time.unscaledDeltaTime;
            else brushYawTarget += turn * 120f * Time.unscaledDeltaTime;
        }
        float k = 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime);
        brushYaw = Mathf.Lerp(brushYaw, brushYawTarget, k);
        if (selected != null && !Mathf.Approximately(selected.data.yaw, selYawTarget))
        {
            selected.data.yaw = Mathf.Abs(selected.data.yaw - selYawTarget) < 0.05f ? selYawTarget : Mathf.Lerp(selected.data.yaw, selYawTarget, k);
            Rebuild(selected);
            dirty = true;
        }
    }

    // ------------------------------------------------------------------
    // Panel
    // ------------------------------------------------------------------

    private static void PlaceTL(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    private void BuildPanel()
    {
        PixelUIKit.EnsureEventSystem();
        // The controls live in the Mock up mode window's first tab (PixelDevMockWindow): a 470-wide column centred in it.
        RectTransform tabRoot = PixelDevMockWindow.TabRoot(0);
        GameObject windowGo = new GameObject("Shapes Content", typeof(RectTransform));
        windowGo.transform.SetParent(tabRoot, false);
        RectTransform wr = windowGo.GetComponent<RectTransform>();
        wr.anchorMin = wr.anchorMax = wr.pivot = new Vector2(0.5f, 1f);
        wr.anchoredPosition = Vector2.zero;
        wr.sizeDelta = new Vector2(470f, PixelDevMockWindow.ContentHeight);

        Color text = new Color(0.92f, 0.94f, 1f, 1f), box = new Color(0.16f, 0.2f, 0.3f, 1f), btn = new Color(0.25f, 0.45f, 0.8f, 1f);
        float y = 8f;

        // Shape + mode
        kindDropdown = PixelUIKit.CreateDropdown(font, windowGo.transform, "Kind", new Vector2(200f, 46f), box, box, text, 26f);
        kindMap = PixelUIKit.AddSortedOptions(kindDropdown, Enum.GetNames(typeof(ShapeKind)));   // (alphabetical; enum names are in value order)
        PlaceTL(kindDropdown.GetComponent<RectTransform>(), 20f, y, 200f, 46f);
        modeDropdown = PixelUIKit.CreateDropdown(font, windowGo.transform, "Mode", new Vector2(200f, 46f), box, box, text, 26f);
        modeMap = PixelUIKit.AddSortedOptions(modeDropdown, Enum.GetNames(typeof(ShapeMode)));
        PlaceTL(modeDropdown.GetComponent<RectTransform>(), 250f, y, 200f, 46f);
        kindDropdown.onValueChanged.AddListener(v => { if (!buildingUi && selected != null) { selected.data.kind = PickedKind; Rebuild(selected); dirty = true; } });
        modeDropdown.onValueChanged.AddListener(v => { if (!buildingUi && selected != null) { selected.data.mode = PickedMode; Rebuild(selected); dirty = true; } });
        y += 62f;

        // Colour / delete / copy
        Button colour = PixelUIKit.CreateButton(font, windowGo.transform, "Colour", "Colour", new Vector2(140f, 46f), btn, text, 24f);
        PlaceTL(colour.GetComponent<RectTransform>(), 20f, y, 140f, 46f);
        colourLabel = colour.GetComponentInChildren<TMP_Text>();
        colour.onClick.AddListener(() =>
        {
            brushColor = (brushColor + 1) % Palette.Length;
            if (selected != null) { selected.data.color = brushColor; Rebuild(selected); dirty = true; }
            Refresh();
        });

        Button delete = PixelUIKit.CreateButton(font, windowGo.transform, "Delete", "Delete", new Vector2(140f, 46f), new Color(0.65f, 0.25f, 0.25f, 1f), text, 24f);
        PlaceTL(delete.GetComponent<RectTransform>(), 170f, y, 140f, 46f);
        delete.onClick.AddListener(DeleteSelected);
        Button copy = PixelUIKit.CreateButton(font, windowGo.transform, "Copy", "Copy layout", new Vector2(130f, 46f), new Color(0.25f, 0.6f, 0.4f, 1f), text, 22f);
        PlaceTL(copy.GetComponent<RectTransform>(), 320f, y, 130f, 46f);
        copy.onClick.AddListener(CopyLayout);
        y += 58f;

        Button reset = PixelUIKit.CreateButton(font, windowGo.transform, "Reset", "RESET ALL SHAPES", new Vector2(430f, 46f), new Color(0.75f, 0.2f, 0.2f, 1f), text, 24f);
        PlaceTL(reset.GetComponent<RectTransform>(), 20f, y, 430f, 46f);
        reset.onClick.AddListener(ResetAll);
        y += 58f;

        TMP_Text hint = PixelUIKit.CreateText(font, windowGo.transform, "Hint", "Click floor: place (ghost shows where)   Click shape: select / drag\nWheel or Q / E: rotate   Ctrl + wheel: size   Delete: remove",
                                              20f, TextAlignmentOptions.TopLeft, FontStyles.Normal, new Color(0.7f, 0.75f, 0.85f, 1f));
        PlaceTL(hint.rectTransform, 20f, y, 430f, 84f);
        y += 92f;

        readout = PixelUIKit.CreateText(font, windowGo.transform, "Readout", "", 19f, TextAlignmentOptions.TopLeft, FontStyles.Normal, text);
        PlaceTL(readout.rectTransform, 20f, y, 430f, PixelDevMockWindow.ContentHeight - y - 12f);
        readout.overflowMode = TextOverflowModes.Truncate;
        Refresh();
    }

    /// <summary>A see-through copy of the brush shape follows the mouse over the floor, showing what a click would place (kind, size, turn, colour).</summary>
    private void UpdateGhost(bool show)
    {
        if (!show || cam == null || kindDropdown == null)
        {
            if (ghost != null && ghost.activeSelf) ghost.SetActive(false);
            return;
        }
        if (!FloorPoint(cam.ScreenPointToRay(PointerPosition()), out Vector3 point))
        {
            if (ghost != null && ghost.activeSelf) ghost.SetActive(false);
            return;
        }
        if (ghost == null)
        {
            ghost = new GameObject("Shape Ghost");
            ghostFilter = ghost.AddComponent<MeshFilter>();
            MeshRenderer mr = ghost.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            Shader shader = PixelShaders.SpriteDefault();
            ghostMat = new Material(shader != null ? shader : Shader.Find("Sprites/Default"));
            mr.sharedMaterial = ghostMat;
        }
        ShapeKind kind = (ShapeKind)Mathf.Clamp(PickedKind, 0, Enum.GetValues(typeof(ShapeKind)).Length - 1);
        ghostFilter.sharedMesh = MeshOf(kind);
        ghost.transform.rotation = Quaternion.Euler(0f, brushYaw, 0f);
        ghost.transform.localScale = BaseScale(kind) * (Unit * Mathf.Max(0.05f, brushSize));
        ghost.transform.position = point;
        Renderer r = ghost.GetComponent<Renderer>();
        ghost.transform.position = point + Vector3.up * r.bounds.extents.y;   // sits on the floor like the placed shape will
        Color c = Palette[Mathf.Abs(brushColor) % Palette.Length];
        ghostMat.color = new Color(c.r, c.g, c.b, 0.45f);
        if (!ghost.activeSelf) ghost.SetActive(true);
    }

    private void Refresh()
    {
        if (readout == null) return;
        if (colourLabel != null) PixelUIKit.SetText(colourLabel, ColorNames[brushColor % ColorNames.Length]);
        StringBuilder sb = new StringBuilder();
        sb.Append("Shapes: ").Append(shapes.Count).Append("   brush size ").Append(brushSize.ToString("0.00")).Append("  turn ").Append(Mathf.Round(((brushYaw % 360f) + 360f) % 360f)).Append('\n');
        int max = 16;
        for (int i = 0; i < shapes.Count && i < max; i++) sb.Append(Line(shapes[i], shapes[i] == selected)).Append('\n');
        if (shapes.Count > max) sb.Append("... and ").Append(shapes.Count - max).Append(" more (Copy layout lists them all)");
        PixelUIKit.SetText(readout, sb.ToString());
    }

    private string Line(Shape s, bool isSelected)
    {
        ShapeData d = s.data;
        return (isSelected ? "> " : "  ") + "#" + d.id + " " + (ShapeKind)d.kind + " " + (ShapeMode)d.mode + "  x " + d.pos.x.ToString("0.00") + " z " + d.pos.z.ToString("0.00")
             + " y " + d.pos.y.ToString("0.00") + "  rot " + Mathf.Round(((d.yaw % 360f) + 360f) % 360f) + "  size " + d.size.ToString("0.00") + "  " + ColorNames[Mathf.Abs(d.color) % ColorNames.Length];
    }

    private void CopyLayout()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("Shape layout (world units; size is a multiple of the main pixel's width ").Append(Unit.ToString("0.###")).Append(", rot = degrees round the up axis)\n");
        foreach (Shape s in shapes) sb.Append(Line(s, false).Substring(2)).Append('\n');
        GUIUtility.systemCopyBuffer = sb.ToString();
        PixelHints.Announce("Shape layout copied", "The shape layout (" + shapes.Count + " shapes) is on your clipboard.");
    }
}
