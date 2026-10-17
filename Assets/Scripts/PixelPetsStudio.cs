// ---- System notes (moved here from CLAUDE.md) ----
// PixelPetsStudio.cs
//   Part of partial class PixelPets: the Outfit Studio (button 'Design outfits' in the Customize Pet window, own canvas
//   customizeSortingOrder + 20, PixelWindows 170). The player builds custom outfits from basic shapes (CPiece: cube / sphere /
//   cylinder / capsule / cone, position, size, turn, colour (16 StudioColors), opacity, optional mirror = a second copy on the other
//   side) on a dummy: a plain grey cube or the real-look model of ANY pet they own (studioDummy; some pets look different), in a
//   hidden studio (StudioStageHeight) shown by a RawImage (drag to turn, wheel to zoom; a yellow marker shows the selected shape's
//   centre; CLICK a shape in the view to select it and DRAG it: the move locks to the X / Y / Z axis that points most along the drag on
//   screen, a coloured line shows that axis, the position slider follows - StudioPointerDown / StudioPieceDrag, colliders on the studio copies only;
//   a drag on empty space still turns the view). Outfits (COutfit: name, forType = one pet type or -1 any) are edited live (every change saved) with a piece list, nine
//   sliders, swatches, duplicate / delete, New / Delete outfit, and 'Wear this outfit'. Outfit numbers 10000 + n are custom outfits
//   (CustomBase; BuildOutfit handles them, so live pets, title-screen pets and previews all use them; deleting one renumbers / clears
//   pets wearing it); the outfit arrows cycle OutfitChoices(type) = built-ins then the custom ones that fit that pet. Saved as JSON in
//   PlayerPrefs PixelClicker.Setting.PetOutfits (per save file via PixelSettingsSync, reloaded by PixelPets.ReloadOutfitsFromPrefs).
// ---------------------------------------------------
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Part of <c>partial class PixelPets</c>: the <b>Outfit Studio</b>. From the Customize Pet window, 'Design outfits' opens a studio where the player
/// builds their OWN outfits out of basic shapes (cube, sphere, cylinder, capsule, cone) on a DUMMY: a plain cube, or the model of any pet they
/// own (some pets look different - the ghost is a blob, the seed pet a sprout - so the dummy can be switched to check the fit). Each shape has a
/// position, size, turn, colour and opacity, and an optional mirror (a second copy on the other side, for ears, horns and wings). An outfit can be
/// limited to one pet type. Custom outfits are saved per save file (PlayerPrefs <c>PixelClicker.Setting.PetOutfits</c>) and appear after the built-in
/// outfits when cycling a pet's outfits (outfit numbers 10000 + n are custom outfits, so saved pets keep working).
/// </summary>
public partial class PixelPets
{
    private const int CustomBase = 10000;
    private const string PrefCustom = "PixelClicker.Setting.PetOutfits";

    [Serializable]
    private class CPiece
    {
        public int shape;                         // 0 cube, 1 sphere, 2 cylinder, 3 capsule, 4 cone
        public float px, py = 0.55f, pz;
        public float sx = 0.3f, sy = 0.3f, sz = 0.3f;
        public float rx, ry, rz;
        public int color = 8;
        public float alpha = 1f;
        public bool mirror;
    }

    [Serializable]
    private class COutfit
    {
        public string name = "Custom";
        public int forType = -1;                  // a PixelType number, or -1 = any pet
        public List<CPiece> pieces = new List<CPiece>();
    }

    [Serializable]
    private class CBlob { public List<COutfit> outfits = new List<COutfit>(); }

    private List<COutfit> customOutfits = new List<COutfit>();
    private bool customsLoaded;

    private static readonly string[] ShapeNames = { "Cube", "Sphere", "Cylinder", "Capsule", "Cone" };

    private static readonly Color[] StudioColors =
    {
        new Color(0.9f, 0.2f, 0.25f), new Color(1f, 0.6f, 0.15f), new Color(1f, 0.85f, 0.2f), new Color(0.3f, 0.8f, 0.35f),
        new Color(0.15f, 0.75f, 0.75f), new Color(0.25f, 0.45f, 0.95f), new Color(0.6f, 0.35f, 0.9f), new Color(0.95f, 0.45f, 0.75f),
        new Color(0.95f, 0.95f, 0.95f), new Color(0.6f, 0.62f, 0.68f), new Color(0.35f, 0.36f, 0.42f), new Color(0.15f, 0.15f, 0.2f),
        new Color(0.5f, 0.32f, 0.18f), new Color(0.85f, 0.7f, 0.5f), new Color(0.1f, 0.4f, 0.2f), new Color(0.5f, 0.05f, 0.1f),
    };

    private List<COutfit> Customs()
    {
        if (!customsLoaded) { customsLoaded = true; LoadCustomOutfits(false); }
        return customOutfits;
    }

    /// <summary>Called after a save is loaded: the custom outfits belong to the save file.</summary>
    public static void ReloadOutfitsFromPrefs()
    {
        if (Instance == null) return;
        Instance.customsLoaded = true;
        Instance.LoadCustomOutfits(true);
    }

    private void LoadCustomOutfits(bool reapply)
    {
        customOutfits = new List<COutfit>();
        string json = PixelPrefs.GetString(PrefCustom, "");
        if (!string.IsNullOrEmpty(json))
        {
            try { CBlob blob = JsonUtility.FromJson<CBlob>(json); if (blob != null && blob.outfits != null) customOutfits = blob.outfits; }
            catch (Exception) { customOutfits = new List<COutfit>(); }
        }
        if (reapply) foreach (Pet p in pets) ApplyOutfit(p);
    }

    private void SaveCustomOutfits()
    {
        PixelPrefs.SetString(PrefCustom, JsonUtility.ToJson(new CBlob { outfits = customOutfits }));
    }

    /// <summary>The outfit numbers a pet of this type can cycle through: every built-in outfit, then the custom ones that fit it.</summary>
    private List<int> OutfitChoices(PixelClicker.PixelType type)
    {
        List<int> choices = new List<int>();
        int count = OutfitList().Count;
        for (int i = 0; i < count; i++) choices.Add(i);
        List<COutfit> customs = Customs();
        for (int c = 0; c < customs.Count; c++)
            if (customs[c].forType < 0 || customs[c].forType == (int)type) choices.Add(CustomBase + c);
        return choices;
    }

    private string OutfitNameOf(int value)
    {
        if (value >= CustomBase)
        {
            int c = value - CustomBase;
            return c >= 0 && c < Customs().Count ? Customs()[c].name : "?";
        }
        List<Outfit> list = OutfitList();
        return value >= 0 && value < list.Count ? list[value].name : "?";
    }

    /// <summary>Builds a custom outfit as a child "Outfit" of 'parent' ('size' = the pet's edge length).</summary>
    private void BuildCustomOutfit(Transform parent, COutfit o, float size)
    {
        GameObject root = new GameObject("Outfit");
        root.transform.SetParent(parent, false);
        root.transform.localScale = Vector3.one * size;
        foreach (CPiece p in o.pieces)
        {
            AddCustomPiece(root.transform, p, false);
            if (p.mirror) AddCustomPiece(root.transform, p, true);
        }
    }

    private GameObject AddCustomPiece(Transform root, CPiece p, bool mirrored)
    {
        Color c = StudioColors[Mathf.Abs(p.color) % StudioColors.Length];
        c.a = Mathf.Clamp(p.alpha, 0.1f, 1f);
        float m = mirrored ? -1f : 1f;
        Vector3 pos = new Vector3(p.px * m, p.py, p.pz);
        Vector3 euler = new Vector3(p.rx, p.ry * m, p.rz * m);
        Vector3 scale = new Vector3(p.sx, p.sy, p.sz);
        switch (p.shape)
        {
            case 1: return Prim(root, PrimitiveType.Sphere, pos, scale, c, euler);
            case 2: return Prim(root, PrimitiveType.Cylinder, pos, new Vector3(scale.x, scale.y * 0.5f, scale.z), c, euler);   // (the primitive is 2 high)
            case 3: return Prim(root, PrimitiveType.Capsule, pos, new Vector3(scale.x, scale.y * 0.5f, scale.z), c, euler);
            case 4: return Prim(root, PrimitiveType.Cube, pos, scale, c, euler, true);
            default: return Prim(root, PrimitiveType.Cube, pos, scale, c, euler);
        }
    }

    // ------------------------------------------------------------------
    // The studio window
    // ------------------------------------------------------------------

    private bool studioOpen;
    private GameObject studioCanvas, studioWorld;
    private RenderTexture studioRT;
    private Camera studioCam;
    private Transform studioModel;
    private float studioYaw = 20f, studioPitch = 10f, studioZoom = 4.6f;
    private bool studioUpdating;
    private int studioDummy = -1;       // -1 = the plain dummy, else a PixelType number
    private int studioOutfit = -1;      // index into the custom outfits
    private int studioPiece = -1;
    private Pet studioPet;
    private TMP_Dropdown dummyDropdown, outfitDropdown, forDropdown;
    private TMP_InputField studioNameField;
    private Slider[] studioSliders;
    private TMP_Text[] studioSliderValues;
    private Slider studioAlpha;
    private TMP_Text studioMirrorLabel;
    private Image[] studioSwatches;
    private RectTransform pieceListContent;
    private ScrollRect pieceListScroll;
    private GameObject pieceListBar;
    private readonly List<PixelClicker.PixelType> studioPetTypes = new List<PixelClicker.PixelType>();
    private const float StudioStageHeight = -3300f;

    private static readonly string[] SliderNames = { "Move X", "Move Y", "Move Z", "Size X", "Size Y", "Size Z", "Turn X", "Turn Y", "Turn Z" };
    private static readonly float[] SliderMin = { -1f, -1f, -1f, 0.02f, 0.02f, 0.02f, -180f, -180f, -180f };
    private static readonly float[] SliderMax = { 1f, 1f, 1f, 1.5f, 1.5f, 1.5f, 180f, 180f, 180f };

    private COutfit CurrentOutfit => studioOutfit >= 0 && studioOutfit < Customs().Count ? Customs()[studioOutfit] : null;
    private CPiece CurrentPiece => CurrentOutfit != null && studioPiece >= 0 && studioPiece < CurrentOutfit.pieces.Count ? CurrentOutfit.pieces[studioPiece] : null;

    private void OpenStudio(Pet p)
    {
        if (studioOpen || p == null) return;
        studioOpen = true;
        studioPet = p;
        studioDummy = (int)p.type;
        studioOutfit = p.outfit >= CustomBase && p.outfit - CustomBase < Customs().Count ? p.outfit - CustomBase : (Customs().Count > 0 ? 0 : -1);
        studioPiece = CurrentOutfit != null && CurrentOutfit.pieces.Count > 0 ? 0 : -1;

        studioPetTypes.Clear();
        foreach (Pet q in pets) studioPetTypes.Add(q.type);

        BuildStudioStage();
        BuildStudioWindow();
        PixelWindows.Register(studioKey, 170, () => studioOpen, CloseStudio);
        RefreshStudio();
        PixelAudio.Play("ui_click");
    }

    private readonly object studioKey = new object();

    private void BuildStudioStage()
    {
        studioWorld = new GameObject("Pet Outfit Studio");
        studioWorld.transform.position = new Vector3(0f, StudioStageHeight, 0f);
        studioRT = new RenderTexture(800, 620, 24, RenderTextureFormat.ARGB32) { name = "Outfit Studio" };
        GameObject camGo = new GameObject("Studio Camera");
        camGo.transform.SetParent(studioWorld.transform, false);
        studioCam = camGo.AddComponent<Camera>();
        studioCam.clearFlags = CameraClearFlags.SolidColor;
        studioCam.backgroundColor = previewBackground;
        studioCam.fieldOfView = 30f;
        studioCam.nearClipPlane = 0.1f;
        studioCam.farClipPlane = 40f;
        studioCam.targetTexture = studioRT;
        studioCam.allowHDR = false;
        studioCam.allowMSAA = true;
        studioModel = new GameObject("Model").transform;
        studioModel.SetParent(studioWorld.transform, false);
        PlaceStudioCamera();
    }

    private void PlaceStudioCamera()
    {
        if (studioCam == null) return;
        studioCam.transform.localPosition = new Vector3(0f, 0.3f, studioZoom);
        studioCam.transform.LookAt(studioWorld.transform.position + new Vector3(0f, 0.25f, 0f));
    }

    private void RebuildStudioModel()
    {
        if (studioModel == null) return;
        for (int i = studioModel.childCount - 1; i >= 0; i--) { Transform c = studioModel.GetChild(i); c.SetParent(null); Destroy(c.gameObject); }
        GameObject root = new GameObject("Dummy");
        root.transform.SetParent(studioModel, false);
        if (studioDummy >= 0)
        {
            int tier = TierOf((PixelClicker.PixelType)studioDummy);
            GameObject model = tier >= 0 ? clicker.CreateDisplayPixel(tier, root.transform, 1f) : null;
            if (model != null) { model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity; }
        }
        else Prim(root.transform, PrimitiveType.Cube, Vector3.zero, Vector3.one, new Color(0.62f, 0.64f, 0.7f));

        // The outfit's shapes, each with a collider so a click in the view can pick it (studioPick: collider -> piece index, mirrored copy flagged).
        studioPick.Clear();
        studioRoot = root.transform;
        COutfit outfit = CurrentOutfit;
        if (outfit != null)
            for (int i = 0; i < outfit.pieces.Count; i++)
            {
                MakePickable(AddCustomPiece(root.transform, outfit.pieces[i], false), i, false);
                if (outfit.pieces[i].mirror) MakePickable(AddCustomPiece(root.transform, outfit.pieces[i], true), i, true);
            }
        CPiece piece = CurrentPiece;
        if (piece != null)
        {
            // A small bright marker where the selected shape is centred.
            Vector3 centre = new Vector3(piece.px, piece.py, piece.pz);
            GameObject marker = Prim(root.transform, PrimitiveType.Sphere, centre, Vector3.one * 0.06f, new Color(1f, 0.95f, 0.2f));
            marker.name = "Marker";
            // While a shape is dragged: a long line along the axis it moves on (red X, green Y, blue Z).
            if (dragAxis >= 0)
            {
                Color[] axisColors = { new Color(1f, 0.3f, 0.3f), new Color(0.35f, 1f, 0.4f), new Color(0.35f, 0.6f, 1f) };
                Vector3 size = dragAxis == 0 ? new Vector3(3f, 0.015f, 0.015f) : dragAxis == 1 ? new Vector3(0.015f, 3f, 0.015f) : new Vector3(0.015f, 0.015f, 3f);
                Prim(root.transform, PrimitiveType.Cube, centre, size, axisColors[dragAxis]).name = "Axis";
            }
        }
    }

    // ---- Click a shape in the view and drag it along an axis ----
    private readonly Dictionary<Collider, Vector2Int> studioPick = new Dictionary<Collider, Vector2Int>();   // collider -> (piece index, 1 = the mirrored copy)
    private Transform studioRoot;
    private RectTransform studioViewRect;
    private bool draggingPiece, dragMirrored;
    private int dragAxis = -1;                  // -1 = not chosen yet (or not dragging), 0 x, 1 y, 2 z
    private Vector2 dragStartPointer;           // in the view's local units
    private float dragStartValue;

    private void MakePickable(GameObject g, int index, bool mirrored)
    {
        if (g == null) return;
        MeshFilter mf = g.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;
        MeshCollider mc = g.AddComponent<MeshCollider>();
        mc.sharedMesh = mf.sharedMesh;
        studioPick[mc] = new Vector2Int(index, mirrored ? 1 : 0);
    }

    /// <summary>The pointer in the view's local units (bottom left = 0,0); false when it is outside the view.</summary>
    private bool ViewPoint(Vector2 screen, out Vector2 local, out Vector2 viewport)
    {
        local = viewport = Vector2.zero;
        if (studioViewRect == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(studioViewRect, screen, null, out Vector2 p)) return false;
        Rect r = studioViewRect.rect;
        local = p - r.min;
        viewport = new Vector2(local.x / r.width, local.y / r.height);
        return true;
    }

    /// <summary>Where a studio-world point lands in the view's local units.</summary>
    private Vector2 WorldToView(Vector3 world)
    {
        Vector3 vp = studioCam.WorldToViewportPoint(world);
        Rect r = studioViewRect.rect;
        return new Vector2(vp.x * r.width, vp.y * r.height);
    }

    private void StudioPointerDown(PointerEventData e)
    {
        draggingPiece = false;
        dragAxis = -1;
        if (studioCam == null || CurrentOutfit == null || !ViewPoint(e.position, out Vector2 local, out Vector2 vp)) return;
        Ray ray = studioCam.ViewportPointToRay(new Vector3(vp.x, vp.y, 0f));
        float best = float.PositiveInfinity;
        Vector2Int hitPiece = new Vector2Int(-1, 0);
        foreach (RaycastHit h in Physics.RaycastAll(ray, 40f, ~0, QueryTriggerInteraction.Collide))
            if (h.distance < best && studioPick.TryGetValue(h.collider, out Vector2Int pick)) { best = h.distance; hitPiece = pick; }
        if (hitPiece.x < 0) return;   // empty space: the drag turns the view as before
        draggingPiece = true;
        dragMirrored = hitPiece.y == 1;
        dragStartPointer = local;
        if (studioPiece != hitPiece.x) { studioPiece = hitPiece.x; RefreshStudio(); }
    }

    private void StudioPieceDrag(PointerEventData e)
    {
        CPiece p = CurrentPiece;
        if (p == null || studioRoot == null || !ViewPoint(e.position, out Vector2 local, out _)) return;
        Vector2 moved = local - dragStartPointer;
        Vector3 centre = studioRoot.TransformPoint(new Vector3(dragMirrored ? -p.px : p.px, p.py, p.pz));
        if (dragAxis < 0)
        {
            if (moved.magnitude < 6f) return;   // wait for a clear direction, then lock to the axis that points most along it on screen
            float bestDot = -1f;
            for (int a = 0; a < 3; a++)
            {
                Vector2 sd = WorldToView(centre + studioRoot.TransformVector(Axis(a)) * 0.2f) - WorldToView(centre);
                if (sd.sqrMagnitude < 1e-4f) continue;
                float d = Mathf.Abs(Vector2.Dot(sd.normalized, moved.normalized));
                if (d > bestDot) { bestDot = d; dragAxis = a; }
            }
            if (dragAxis < 0) return;
            dragStartValue = dragAxis == 0 ? p.px : dragAxis == 1 ? p.py : p.pz;
            dragStartPointer = local;
            moved = Vector2.zero;
        }
        // How far one unit along the axis moves on screen; the pointer's movement along that line becomes the new value.
        Vector2 perUnit = WorldToView(centre + studioRoot.TransformVector(Axis(dragAxis))) - WorldToView(centre);
        if (perUnit.sqrMagnitude < 1e-4f) return;
        float units = Vector2.Dot(moved, perUnit) / perUnit.sqrMagnitude;
        if (dragAxis == 0 && dragMirrored) units = -units;   // (the mirrored copy moves the other way along X)
        float value = Mathf.Clamp(dragStartValue + units, SliderMin[dragAxis], SliderMax[dragAxis]);
        if (dragAxis == 0) p.px = value; else if (dragAxis == 1) p.py = value; else p.pz = value;
        studioSliders[dragAxis].SetValueWithoutNotify(value);
        studioSliderValues[dragAxis].text = value.ToString("0.00");
        RebuildStudioModel();
    }

    private void StudioPieceDragEnd()
    {
        if (!draggingPiece) return;
        draggingPiece = false;
        dragAxis = -1;
        SaveCustomOutfits();
        RebuildStudioModel();
    }

    private static Vector3 Axis(int a) => a == 0 ? Vector3.right : a == 1 ? Vector3.up : Vector3.forward;

    private void Put(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    private Button StudioButton(Transform parent, string label, float x, float y, float w, float h, Color color, Action onClick, float size = 24f)
    {
        Button b = PixelUIKit.CreateButton(font, parent, label, label, new Vector2(w, h), color, Color.white, size);
        Put(b.GetComponent<RectTransform>(), x, y, w, h);
        b.onClick.AddListener(() => onClick());
        return b;
    }

    private TMP_Text StudioLabel(Transform parent, string text, float x, float y, float w, float h, float size = 22f, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
    {
        TMP_Text t = PixelUIKit.CreateText(font, parent, "Label", text, size, align, FontStyles.Normal, Color.white);
        Put(t.rectTransform, x, y, w, h);
        return t;
    }

    private void BuildStudioWindow()
    {
        PixelUIKit.EnsureEventSystem();
        studioCanvas = PixelUIKit.CreateCanvas("Pet Outfit Studio Canvas", customizeSortingOrder + 20, referenceResolution, true);
        GameObject dim = new GameObject("Dim", typeof(RectTransform), typeof(Image));
        dim.transform.SetParent(studioCanvas.transform, false);
        dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
        PixelUIKit.Stretch(dim.GetComponent<RectTransform>());

        GameObject box = new GameObject("Box", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(dim.transform, false);
        PixelUIKit.StyleWindow(box.GetComponent<Image>(), panelColor);
        RectTransform br = box.GetComponent<RectTransform>();
        br.anchorMin = br.anchorMax = br.pivot = new Vector2(0.5f, 0.5f);
        br.sizeDelta = new Vector2(1800f, 980f);
        Transform t = box.transform;
        Color box2 = new Color(0.2f, 0.2f, 0.27f, 1f), grey = new Color(0.32f, 0.36f, 0.48f, 1f), red = new Color(0.65f, 0.25f, 0.25f, 1f);

        TMP_Text title = PixelUIKit.CreateText(font, t, "Title", "OUTFIT STUDIO", 46f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, titleColor);
        Put(title.rectTransform, 40f, 20f, 800f, 64f);

        // ---- Left: the 3D view + the dummy ----
        GameObject view = new GameObject("View", typeof(RectTransform), typeof(RawImage), typeof(EventTrigger));
        view.transform.SetParent(t, false);
        view.GetComponent<RawImage>().texture = studioRT;
        Put(view.GetComponent<RectTransform>(), 30f, 100f, 800f, 620f);
        studioViewRect = view.GetComponent<RectTransform>();
        EventTrigger trig = view.GetComponent<EventTrigger>();
        AddTrigger(trig, EventTriggerType.PointerDown, data => StudioPointerDown((PointerEventData)data));   // on a shape: select it, the drag moves it
        AddTrigger(trig, EventTriggerType.PointerUp, _ => StudioPieceDragEnd());
        AddTrigger(trig, EventTriggerType.EndDrag, _ => StudioPieceDragEnd());
        AddTrigger(trig, EventTriggerType.Drag, data =>
        {
            if (draggingPiece) { StudioPieceDrag((PointerEventData)data); return; }
            Vector2 d = ((PointerEventData)data).delta;
            studioYaw += d.x * 0.6f;
            studioPitch = Mathf.Clamp(studioPitch - d.y * 0.6f, -85f, 85f);
        });
        AddTrigger(trig, EventTriggerType.Scroll, data =>
        {
            studioZoom = Mathf.Clamp(studioZoom - ((PointerEventData)data).scrollDelta.y * 0.4f, 2f, 10f);
            PlaceStudioCamera();
        });

        StudioLabel(t, "Dummy:", 30f, 736f, 120f, 46f, 24f);
        dummyDropdown = PixelUIKit.CreateDropdown(font, t, "Dummy", new Vector2(420f, 46f), box2, box2, Color.white, 24f);
        Put(dummyDropdown.GetComponent<RectTransform>(), 150f, 736f, 420f, 46f);
        List<string> dummies = new List<string> { "Plain dummy (a grey cube)" };
        foreach (PixelClicker.PixelType type in studioPetTypes) dummies.Add(PetName(type));
        dummyDropdown.ClearOptions();
        dummyDropdown.AddOptions(dummies);
        dummyDropdown.onValueChanged.AddListener(v =>
        {
            if (studioUpdating) return;
            studioDummy = v <= 0 ? -1 : (int)studioPetTypes[v - 1];
            RebuildStudioModel();
        });
        StudioLabel(t, "Drag the view to turn it, mouse wheel to zoom. Build on any pet you own: some look different (blob, sprout, glass).", 30f, 792f, 800f, 70f, 20f, TextAlignmentOptions.TopLeft);

        StudioButton(t, "Wear this outfit", 30f, 880f, 390f, 70f, new Color(0.25f, 0.6f, 0.4f, 1f), WearStudioOutfit, 28f);
        StudioButton(t, "Done", 440f, 880f, 390f, 70f, buttonColor, CloseStudio, 28f);

        // ---- Right: outfits ----
        float rx = 860f;
        outfitDropdown = PixelUIKit.CreateDropdown(font, t, "Outfit", new Vector2(420f, 46f), box2, box2, Color.white, 24f);
        Put(outfitDropdown.GetComponent<RectTransform>(), rx, 100f, 420f, 46f);
        outfitDropdown.onValueChanged.AddListener(v =>
        {
            if (studioUpdating) return;
            studioOutfit = v < Customs().Count ? v : -1;
            studioPiece = CurrentOutfit != null && CurrentOutfit.pieces.Count > 0 ? 0 : -1;
            RefreshStudio();
        });
        StudioButton(t, "New outfit", rx + 430f, 100f, 230f, 46f, new Color(0.25f, 0.6f, 0.4f, 1f), NewCustomOutfit, 22f);
        StudioButton(t, "Delete outfit", rx + 670f, 100f, 240f, 46f, red, DeleteCustomOutfit, 22f);

        studioNameField = PixelUIKit.CreateInputField(font, t, "Name", new Vector2(420f, 46f), box2, Color.white, 24f, "outfit name");
        studioNameField.characterLimit = 24;
        Put(studioNameField.GetComponent<RectTransform>(), rx, 156f, 420f, 46f);
        studioNameField.onEndEdit.AddListener(s =>
        {
            if (studioUpdating || CurrentOutfit == null || string.IsNullOrWhiteSpace(s)) return;
            CurrentOutfit.name = s.Trim();
            SaveCustomOutfits();
            RefreshStudio();
        });
        StudioLabel(t, "Fits:", rx + 430f, 156f, 70f, 46f, 22f);
        forDropdown = PixelUIKit.CreateDropdown(font, t, "For", new Vector2(400f, 46f), box2, box2, Color.white, 22f);
        Put(forDropdown.GetComponent<RectTransform>(), rx + 500f, 156f, 410f, 46f);
        List<string> forOptions = new List<string> { "Any pet" };
        foreach (PixelClicker.PixelType type in studioPetTypes) forOptions.Add("Only " + PetName(type));
        forDropdown.ClearOptions();
        forDropdown.AddOptions(forOptions);
        forDropdown.onValueChanged.AddListener(v =>
        {
            if (studioUpdating || CurrentOutfit == null) return;
            CurrentOutfit.forType = v <= 0 ? -1 : (int)studioPetTypes[v - 1];
            SaveCustomOutfits();
        });

        // ---- Shapes ----
        StudioLabel(t, "Add a shape:", rx, 212f, 160f, 44f, 22f);
        for (int s = 0; s < ShapeNames.Length; s++)
        {
            int shape = s;
            StudioButton(t, ShapeNames[s], rx + 165f + s * 148f, 212f, 142f, 44f, grey, () => AddShape(shape), 21f);
        }

        // Piece list
        pieceListScroll = PixelUIKit.CreateScrollView(t, "Pieces", new Color(1f, 1f, 1f, 0.35f), 12f, 60f, out pieceListContent, out pieceListBar);
        Put(pieceListScroll.GetComponent<RectTransform>(), rx, 266f, 910f, 150f);

        // ---- Sliders for the selected shape ----
        studioSliders = new Slider[SliderNames.Length];
        studioSliderValues = new TMP_Text[SliderNames.Length];
        for (int i = 0; i < SliderNames.Length; i++)
        {
            int index = i;
            float y = 428f + i * 38f;
            StudioLabel(t, SliderNames[i], rx, y, 140f, 34f, 21f);
            Slider s = PixelUIKit.CreateSlider(t, "Slider " + SliderNames[i], new Color(0.05f, 0.06f, 0.1f, 1f), buttonColor, Color.white);
            Put(s.GetComponent<RectTransform>(), rx + 145f, y + 2f, 640f, 30f);
            s.minValue = SliderMin[i];
            s.maxValue = SliderMax[i];
            s.onValueChanged.AddListener(v => SliderChanged(index, v));
            studioSliders[i] = s;
            studioSliderValues[i] = StudioLabel(t, "", rx + 795f, y, 115f, 34f, 20f, TextAlignmentOptions.MidlineRight);
        }

        // ---- Colour, opacity, mirror, duplicate / delete ----
        studioSwatches = new Image[StudioColors.Length];
        for (int i = 0; i < StudioColors.Length; i++)
        {
            int index = i;
            Button sw = PixelUIKit.CreateButton(font, t, "Swatch " + i, "", new Vector2(50f, 40f), StudioColors[i], Color.white, 10f);
            Put(sw.GetComponent<RectTransform>(), rx + i * 57f, 778f, 52f, 40f);
            sw.onClick.AddListener(() => { if (CurrentPiece != null) { CurrentPiece.color = index; SaveCustomOutfits(); RefreshStudio(); } });
            studioSwatches[i] = sw.GetComponent<Image>();
        }
        StudioLabel(t, "Opacity", rx, 826f, 140f, 34f, 21f);
        studioAlpha = PixelUIKit.CreateSlider(t, "Opacity", new Color(0.05f, 0.06f, 0.1f, 1f), buttonColor, Color.white);
        Put(studioAlpha.GetComponent<RectTransform>(), rx + 145f, 828f, 640f, 30f);
        studioAlpha.minValue = 0.1f;
        studioAlpha.maxValue = 1f;
        studioAlpha.onValueChanged.AddListener(v => { if (!studioUpdating && CurrentPiece != null) { CurrentPiece.alpha = v; SaveCustomOutfits(); RebuildStudioModel(); } });

        Button mirror = StudioButton(t, "Mirror", rx, 876f, 290f, 56f, grey, () =>
        {
            if (CurrentPiece == null) return;
            CurrentPiece.mirror = !CurrentPiece.mirror;
            SaveCustomOutfits();
            RefreshStudio();
        }, 22f);
        studioMirrorLabel = mirror.GetComponentInChildren<TMP_Text>();
        StudioButton(t, "Duplicate shape", rx + 310f, 876f, 290f, 56f, grey, DuplicatePiece, 22f);
        StudioButton(t, "Delete shape", rx + 620f, 876f, 290f, 56f, red, DeletePiece, 22f);
    }

    // ------------------------------------------------------------------
    // Studio actions
    // ------------------------------------------------------------------

    private void NewCustomOutfit()
    {
        COutfit o = new COutfit { name = "Custom " + (Customs().Count + 1) };
        Customs().Add(o);
        studioOutfit = Customs().Count - 1;
        studioPiece = -1;
        SaveCustomOutfits();
        RefreshStudio();
    }

    private void DeleteCustomOutfit()
    {
        if (CurrentOutfit == null) return;
        int removed = studioOutfit;
        Customs().RemoveAt(removed);
        foreach (Pet p in pets)
        {
            if (p.outfit == CustomBase + removed) p.outfit = 0;
            else if (p.outfit > CustomBase + removed) p.outfit--;
            ApplyOutfit(p);
        }
        studioOutfit = Customs().Count > 0 ? Mathf.Min(removed, Customs().Count - 1) : -1;
        studioPiece = CurrentOutfit != null && CurrentOutfit.pieces.Count > 0 ? 0 : -1;
        SaveCustomOutfits();
        RefreshStudio();
    }

    private void AddShape(int shape)
    {
        if (CurrentOutfit == null) NewCustomOutfit();
        CurrentOutfit.pieces.Add(new CPiece { shape = shape, color = 8 });
        studioPiece = CurrentOutfit.pieces.Count - 1;
        SaveCustomOutfits();
        RefreshStudio();
    }

    private void DuplicatePiece()
    {
        CPiece p = CurrentPiece;
        if (p == null) return;
        CPiece copy = JsonUtility.FromJson<CPiece>(JsonUtility.ToJson(p));
        copy.px += 0.06f;
        CurrentOutfit.pieces.Add(copy);
        studioPiece = CurrentOutfit.pieces.Count - 1;
        SaveCustomOutfits();
        RefreshStudio();
    }

    private void DeletePiece()
    {
        if (CurrentPiece == null) return;
        CurrentOutfit.pieces.RemoveAt(studioPiece);
        studioPiece = Mathf.Min(studioPiece, CurrentOutfit.pieces.Count - 1);
        SaveCustomOutfits();
        RefreshStudio();
    }

    private void SliderChanged(int index, float value)
    {
        CPiece p = CurrentPiece;
        if (studioUpdating || p == null) return;
        switch (index)
        {
            case 0: p.px = value; break;
            case 1: p.py = value; break;
            case 2: p.pz = value; break;
            case 3: p.sx = value; break;
            case 4: p.sy = value; break;
            case 5: p.sz = value; break;
            case 6: p.rx = value; break;
            case 7: p.ry = value; break;
            default: p.rz = value; break;
        }
        studioSliderValues[index].text = index < 6 ? value.ToString("0.00") : Mathf.Round(value) + "°";
        SaveCustomOutfits();
        RebuildStudioModel();
    }

    private void WearStudioOutfit()
    {
        if (studioPet == null || CurrentOutfit == null) return;
        studioPet.outfit = CustomBase + studioOutfit;
        ApplyOutfit(studioPet);
        CloseStudio();
    }

    /// <summary>Brings every control in line with the selected outfit / shape and redraws the model.</summary>
    private void RefreshStudio()
    {
        if (!studioOpen || outfitDropdown == null) return;
        studioUpdating = true;

        List<string> names = new List<string>();
        foreach (COutfit o in Customs()) names.Add(o.name);
        names.Add("(no outfit)");
        outfitDropdown.ClearOptions();
        outfitDropdown.AddOptions(names);
        outfitDropdown.SetValueWithoutNotify(studioOutfit >= 0 ? studioOutfit : names.Count - 1);
        outfitDropdown.RefreshShownValue();

        COutfit outfit = CurrentOutfit;
        studioNameField.SetTextWithoutNotify(outfit != null ? outfit.name : "");
        studioNameField.interactable = outfit != null;
        int forIndex = 0;
        if (outfit != null && outfit.forType >= 0) forIndex = studioPetTypes.IndexOf((PixelClicker.PixelType)outfit.forType) + 1;
        forDropdown.SetValueWithoutNotify(Mathf.Max(0, forIndex));
        forDropdown.RefreshShownValue();
        int dummyIndex = studioDummy < 0 ? 0 : studioPetTypes.IndexOf((PixelClicker.PixelType)studioDummy) + 1;
        dummyDropdown.SetValueWithoutNotify(Mathf.Max(0, dummyIndex));
        dummyDropdown.RefreshShownValue();

        // Piece list
        foreach (Transform c in pieceListContent) Destroy(c.gameObject);
        float y = 0f;
        if (outfit != null)
        {
            for (int i = 0; i < outfit.pieces.Count; i++)
            {
                int index = i;
                CPiece p = outfit.pieces[i];
                Button b = PixelUIKit.CreateButton(font, pieceListContent, "Piece " + i, "#" + (i + 1) + "  " + ShapeNames[Mathf.Clamp(p.shape, 0, ShapeNames.Length - 1)] + (p.mirror ? "  (mirrored)" : ""),
                                                   new Vector2(880f, 40f), i == studioPiece ? buttonColor : new Color(0.25f, 0.28f, 0.38f, 1f), Color.white, 21f);
                RectTransform r = b.GetComponent<RectTransform>();
                r.anchorMin = new Vector2(0f, 1f); r.anchorMax = new Vector2(1f, 1f); r.pivot = new Vector2(0.5f, 1f);
                r.offsetMin = new Vector2(4f, 0f); r.offsetMax = new Vector2(-16f, 0f);
                r.sizeDelta = new Vector2(r.sizeDelta.x, 40f);
                r.anchoredPosition = new Vector2(0f, -y);
                b.onClick.AddListener(() => { studioPiece = index; RefreshStudio(); });
                y += 44f;
            }
        }
        PixelUIKit.UpdateScrollView(pieceListScroll, pieceListBar, y, 150f);

        CPiece piece = CurrentPiece;
        float[] values = piece == null ? new float[9] : new[] { piece.px, piece.py, piece.pz, piece.sx, piece.sy, piece.sz, piece.rx, piece.ry, piece.rz };
        for (int i = 0; i < studioSliders.Length; i++)
        {
            studioSliders[i].interactable = piece != null;
            studioSliders[i].SetValueWithoutNotify(Mathf.Clamp(values[i], SliderMin[i], SliderMax[i]));
            studioSliderValues[i].text = piece == null ? "" : i < 6 ? values[i].ToString("0.00") : Mathf.Round(values[i]) + "°";
        }
        studioAlpha.interactable = piece != null;
        studioAlpha.SetValueWithoutNotify(piece != null ? Mathf.Clamp(piece.alpha, 0.1f, 1f) : 1f);
        for (int i = 0; i < studioSwatches.Length; i++)
            studioSwatches[i].transform.localScale = piece != null && Mathf.Abs(piece.color) % StudioColors.Length == i ? Vector3.one * 1.15f : Vector3.one;
        PixelUIKit.SetText(studioMirrorLabel, piece != null && piece.mirror ? "Mirror: ON" : "Mirror: off");

        studioUpdating = false;
        RebuildStudioModel();
    }

    private void UpdateStudio()
    {
        if (!studioOpen || studioModel == null) return;
        studioModel.localRotation = Quaternion.Euler(studioPitch, studioYaw, 0f);
    }

    private void CloseStudio()
    {
        if (!studioOpen) return;
        studioOpen = false;
        PixelWindows.Unregister(studioKey);
        CleanupStudio();
        // The outfits may have changed: every pet and the customize window's preview show the new designs.
        foreach (Pet p in pets) ApplyOutfit(p);
        if (customizing && editPet != null)
        {
            RebuildPreviewModel(editPet);
            UpdateOutfitLabel();
        }
    }

    private void CleanupStudio()
    {
        if (studioCanvas != null) Destroy(studioCanvas);
        studioCanvas = null;
        if (studioWorld != null) Destroy(studioWorld);
        studioWorld = null;
        studioCam = null;
        studioModel = null;
        studioRoot = null;
        studioViewRect = null;
        studioPick.Clear();
        draggingPiece = false;
        dragAxis = -1;
        if (studioRT != null) { studioRT.Release(); Destroy(studioRT); }
        studioRT = null;
        studioOpen = false;
    }
}
