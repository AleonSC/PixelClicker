using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static PixelInput;

/// <summary>
/// Pet names and outfits (part of PixelPets): hovering a pet shows a small name tag with a pencil; the pencil opens the
/// customization window, where the player renames the pet and spins it in a 3D view (click and drag) while the arrows on
/// either side change its outfit. Outfits are made of small accessories (hats, glasses, scarves, capes...) generated from
/// a fixed seed, so every outfit still leaves most of the pet visible and recognisable.
/// </summary>
public partial class PixelPets
{
    [Header("Name tag")]
    [Min(0f)]
    [Tooltip("Seconds the name tag stays after the mouse leaves the pet, so the mouse can reach the pencil.")]
    [SerializeField] private float tagGraceSeconds = 0.35f;

    [Min(8f)]
    [Tooltip("Text size of the name tag.")]
    [SerializeField] private float tagFontSize = 28f;

    [Tooltip("Background colour of the name tag.")]
    [SerializeField] private Color tagColor = new Color(0.08f, 0.08f, 0.12f, 0.9f);

    [Tooltip("Sorting order of the name tag's canvas.")]
    [SerializeField] private int tagSortingOrder = 140;

    [Header("Customization window")]
    [Range(1, 40)]
    [Tooltip("Longest name a pet can have.")]
    [SerializeField] private int maxNameLength = 20;

    [Range(1, 40)]
    [Tooltip("How many generated outfits there are (on top of 'no outfit').")]
    [SerializeField] private int outfitCount = 12;

    [Tooltip("Seed the outfits are generated from. Change it to get a different set (pets keep their outfit number).")]
    [SerializeField] private int outfitSeed = 4242;

    [Min(0f)]
    [Tooltip("How fast the pet turns by itself in the preview while you aren't dragging it (degrees per second).")]
    [SerializeField] private float previewSpinSpeed = 25f;

    [Tooltip("Window title.")]
    [SerializeField] private string customizeTitle = "Customize Pet";

    [Tooltip("Text above the name box.")]
    [SerializeField] private string nameLabelText = "Name";

    [Tooltip("Text of the outfit line under the preview. {0} = number, {1} = how many, {2} = outfit name.")]
    [SerializeField] private string outfitFormat = "Outfit {0} / {1}:  {2}";

    [Tooltip("Name of the empty outfit.")]
    [SerializeField] private string noOutfitName = "Plain";

    [Tooltip("Button that closes the window.")]
    [SerializeField] private string doneText = "Done";

    [Tooltip("Sorting order of the window's canvas (above the dev tools).")]
    [SerializeField] private int customizeSortingOrder = 760;

    [Tooltip("Background colour of the 3D view.")]
    [SerializeField] private Color previewBackground = new Color(0.16f, 0.18f, 0.25f, 1f);

    // ------------------------------------------------------------------
    // Names
    // ------------------------------------------------------------------

    /// <summary>The pet's name for this pixel type: the player's own, else "{Pixel} Pet".</summary>
    public string NameOf(PixelClicker.PixelType type)
    {
        Pet p = pets.Find(x => x.type == type);
        return p != null ? NameOf(p) : PetName(type);
    }

    private string NameOf(Pet p) => string.IsNullOrWhiteSpace(p.customName) ? PetName(p.type) : p.customName;

    // ------------------------------------------------------------------
    // Outfits: accessories generated from a seed
    // ------------------------------------------------------------------

    private enum Acc
    {
        TopHat, PartyHat, WizardHat, Crown, Halo, Headphones, Antennae, Bandana,   // head
        Sunglasses, Mustache,                                                      // face
        BowTie, Scarf,                                                             // neck
        Cape, Wings,                                                               // back
    }

    private static readonly string[] AccNames =
    {
        "Top Hat", "Party Hat", "Wizard Hat", "Crown", "Halo", "Headphones", "Antennae", "Bandana",
        "Sunglasses", "Mustache", "Bow Tie", "Scarf", "Cape", "Wings",
    };

    private static readonly Acc[][] Slots =
    {
        new[] { Acc.TopHat, Acc.PartyHat, Acc.WizardHat, Acc.Crown, Acc.Halo, Acc.Headphones, Acc.Antennae, Acc.Bandana },
        new[] { Acc.Sunglasses, Acc.Mustache },
        new[] { Acc.BowTie, Acc.Scarf },
        new[] { Acc.Cape, Acc.Wings },
    };

    private static readonly Color[] Palette =
    {
        new Color(0.9f, 0.2f, 0.25f), new Color(1f, 0.6f, 0.15f), new Color(1f, 0.85f, 0.2f), new Color(0.3f, 0.8f, 0.35f),
        new Color(0.15f, 0.75f, 0.75f), new Color(0.25f, 0.45f, 0.95f), new Color(0.6f, 0.35f, 0.9f), new Color(0.95f, 0.45f, 0.75f),
        new Color(0.95f, 0.95f, 0.95f), new Color(0.15f, 0.15f, 0.2f),
    };

    private class Outfit
    {
        public string name;
        public List<KeyValuePair<Acc, Color>> pieces = new List<KeyValuePair<Acc, Color>>();
    }

    private List<Outfit> outfits;

    private List<Outfit> OutfitList()
    {
        if (outfits != null && outfits.Count == outfitCount + 1) return outfits;
        outfits = new List<Outfit> { new Outfit { name = noOutfitName } };
        for (int i = 1; i <= outfitCount; i++)
        {
            System.Random rng = new System.Random(outfitSeed + i * 7919);
            // Two or three of the four slots (head / face / neck / back), one accessory each.
            List<int> slots = new List<int> { 0, 1, 2, 3 };
            for (int k = slots.Count - 1; k > 0; k--) { int j = rng.Next(k + 1); int t = slots[k]; slots[k] = slots[j]; slots[j] = t; }
            int count = 2 + rng.Next(2);
            Outfit o = new Outfit();
            int lastColor = -1;
            for (int k = 0; k < count; k++)
            {
                Acc[] choices = Slots[slots[k]];
                Acc acc = choices[rng.Next(choices.Length)];
                int c;
                do { c = rng.Next(Palette.Length); } while (c == lastColor);
                lastColor = c;
                o.pieces.Add(new KeyValuePair<Acc, Color>(acc, Palette[c]));
            }
            o.name = AccNames[(int)o.pieces[0].Key] + " & " + AccNames[(int)o.pieces[1].Key];
            outfits.Add(o);
        }
        return outfits;
    }

    private readonly Dictionary<Color, Material> accessoryMaterials = new Dictionary<Color, Material>();
    private static Mesh coneMesh;

    private Material AccessoryMaterial(Color c)
    {
        if (!accessoryMaterials.TryGetValue(c, out Material m) || m == null)
        {
            m = clicker.CreateVisualMaterial(c, false);
            accessoryMaterials[c] = m;
        }
        return m;
    }

    private static Mesh Cone()
    {
        if (coneMesh != null) return coneMesh;
        const int n = 20;
        Vector3[] v = new Vector3[n + 1];
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2f / n;
            v[i] = new Vector3(Mathf.Cos(a) * 0.5f, -0.5f, Mathf.Sin(a) * 0.5f);
        }
        v[n] = new Vector3(0f, 0.5f, 0f);
        int[] t = new int[n * 3];
        for (int i = 0; i < n; i++) { t[i * 3] = n; t[i * 3 + 1] = i; t[i * 3 + 2] = (i + 1) % n; }
        coneMesh = new Mesh { name = "Pet Cone", vertices = v, triangles = t };
        coneMesh.RecalculateNormals();
        coneMesh.RecalculateBounds();
        return coneMesh;
    }

    // A primitive (or the cone) in unit-cube space under 'parent', without a collider.
    private GameObject Prim(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Color color, Vector3? euler = null, bool cone = false)
    {
        GameObject g;
        if (cone)
        {
            g = new GameObject("Cone", typeof(MeshFilter), typeof(MeshRenderer));
            g.GetComponent<MeshFilter>().sharedMesh = Cone();
        }
        else
        {
            g = GameObject.CreatePrimitive(type);
            Collider c = g.GetComponent<Collider>();
            if (c != null) DestroyImmediate(c);
        }
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        g.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
        MeshRenderer r = g.GetComponent<MeshRenderer>();
        r.sharedMaterial = AccessoryMaterial(color);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return g;
    }

    private void AddAccessory(Transform root, Acc acc, Color c)
    {
        Color dark = Color.Lerp(c, Color.black, 0.5f);
        Color light = Color.Lerp(c, Color.white, 0.45f);
        Color gold = new Color(1f, 0.82f, 0.2f);
        Color black = new Color(0.04f, 0.04f, 0.06f);
        const PrimitiveType Cube = PrimitiveType.Cube, Cyl = PrimitiveType.Cylinder, Sph = PrimitiveType.Sphere;

        switch (acc)
        {
            case Acc.TopHat:
                Prim(root, Cyl, new Vector3(0f, 0.525f, 0f), new Vector3(0.9f, 0.025f, 0.9f), c);
                Prim(root, Cyl, new Vector3(0f, 0.75f, 0f), new Vector3(0.52f, 0.2f, 0.52f), c);
                Prim(root, Cyl, new Vector3(0f, 0.6f, 0f), new Vector3(0.54f, 0.04f, 0.54f), light);
                break;
            case Acc.PartyHat:
                Prim(root, Cube, new Vector3(0f, 0.8f, 0f), new Vector3(0.42f, 0.6f, 0.42f), c, new Vector3(0f, 0f, 8f), true);
                Prim(root, Sph, new Vector3(0.05f, 1.12f, 0f), Vector3.one * 0.11f, light);
                Prim(root, Cyl, new Vector3(0f, 0.53f, 0f), new Vector3(0.44f, 0.02f, 0.44f), dark);
                break;
            case Acc.WizardHat:
                Prim(root, Cyl, new Vector3(0f, 0.51f, 0f), new Vector3(1f, 0.02f, 1f), c);
                Prim(root, Cube, new Vector3(0.03f, 0.98f, 0f), new Vector3(0.6f, 0.95f, 0.6f), c, new Vector3(0f, 0f, 6f), true);
                Prim(root, Cyl, new Vector3(0f, 0.6f, 0f), new Vector3(0.58f, 0.04f, 0.58f), gold);
                break;
            case Acc.Crown:
                Prim(root, Cyl, new Vector3(0f, 0.53f, 0f), new Vector3(0.62f, 0.06f, 0.62f), gold);
                for (int i = 0; i < 5; i++)
                {
                    float a = i * Mathf.PI * 2f / 5f;
                    Prim(root, Cube, new Vector3(Mathf.Cos(a) * 0.27f, 0.68f, Mathf.Sin(a) * 0.27f), new Vector3(0.12f, 0.22f, 0.12f), gold, null, true);
                    Prim(root, Sph, new Vector3(Mathf.Cos(a) * 0.27f, 0.8f, Mathf.Sin(a) * 0.27f), Vector3.one * 0.06f, c);
                }
                break;
            case Acc.Halo:
                for (int i = 0; i < 12; i++)
                {
                    float a = i * Mathf.PI * 2f / 12f;
                    Prim(root, Sph, new Vector3(Mathf.Cos(a) * 0.3f, 0.88f, Mathf.Sin(a) * 0.3f), Vector3.one * 0.09f, gold);
                }
                break;
            case Acc.Headphones:
                for (int i = 0; i <= 8; i++)
                {
                    float a = i * Mathf.PI / 8f;
                    Prim(root, Cube, new Vector3(Mathf.Cos(a) * 0.57f, 0.02f + Mathf.Sin(a) * 0.57f, 0f), new Vector3(0.07f, 0.07f, 0.14f), dark,
                         new Vector3(0f, 0f, a * Mathf.Rad2Deg - 90f));
                }
                foreach (float x in new[] { -0.58f, 0.58f })
                {
                    Prim(root, Cyl, new Vector3(x, 0.02f, 0f), new Vector3(0.3f, 0.05f, 0.3f), c, new Vector3(0f, 0f, 90f));
                    Prim(root, Cyl, new Vector3(x * 1.07f, 0.02f, 0f), new Vector3(0.2f, 0.03f, 0.2f), light, new Vector3(0f, 0f, 90f));
                }
                break;
            case Acc.Antennae:
                foreach (float s in new[] { -1f, 1f })
                {
                    Prim(root, Cyl, new Vector3(s * 0.27f, 0.72f, 0f), new Vector3(0.04f, 0.22f, 0.04f), dark, new Vector3(0f, 0f, -s * 20f));
                    Prim(root, Sph, new Vector3(s * 0.36f, 0.95f, 0f), Vector3.one * 0.13f, c);
                }
                break;
            case Acc.Bandana:
                Prim(root, Cube, new Vector3(0f, 0.36f, 0f), new Vector3(1.04f, 0.18f, 1.04f), c);
                Prim(root, Cube, new Vector3(0f, 0.36f, -0.56f), new Vector3(0.16f, 0.16f, 0.12f), dark, new Vector3(0f, 0f, 45f));
                Prim(root, Cube, new Vector3(-0.1f, 0.24f, -0.6f), new Vector3(0.1f, 0.22f, 0.04f), c, new Vector3(0f, 0f, 15f));
                Prim(root, Cube, new Vector3(0.1f, 0.24f, -0.6f), new Vector3(0.1f, 0.22f, 0.04f), c, new Vector3(0f, 0f, -15f));
                break;
            case Acc.Sunglasses:
                foreach (float x in new[] { -0.2f, 0.2f })
                {
                    Prim(root, Cube, new Vector3(x, 0.1f, 0.52f), new Vector3(0.38f, 0.24f, 0.05f), c);
                    Prim(root, Cube, new Vector3(x, 0.1f, 0.54f), new Vector3(0.3f, 0.17f, 0.04f), black);
                }
                Prim(root, Cube, new Vector3(0f, 0.14f, 0.52f), new Vector3(0.1f, 0.04f, 0.05f), c);
                foreach (float x in new[] { -0.52f, 0.52f })
                    Prim(root, Cube, new Vector3(x, 0.14f, 0.27f), new Vector3(0.03f, 0.04f, 0.48f), c);
                break;
            case Acc.Mustache:
                Prim(root, Cube, new Vector3(-0.1f, -0.06f, 0.52f), new Vector3(0.22f, 0.07f, 0.05f), dark, new Vector3(0f, 0f, 14f));
                Prim(root, Cube, new Vector3(0.1f, -0.06f, 0.52f), new Vector3(0.22f, 0.07f, 0.05f), dark, new Vector3(0f, 0f, -14f));
                Prim(root, Sph, new Vector3(-0.22f, -0.1f, 0.52f), new Vector3(0.08f, 0.08f, 0.05f), dark);
                Prim(root, Sph, new Vector3(0.22f, -0.1f, 0.52f), new Vector3(0.08f, 0.08f, 0.05f), dark);
                break;
            case Acc.BowTie:
                Prim(root, Cube, new Vector3(-0.15f, -0.3f, 0.53f), new Vector3(0.24f, 0.2f, 0.24f), c, new Vector3(0f, 0f, -90f), true);
                Prim(root, Cube, new Vector3(0.15f, -0.3f, 0.53f), new Vector3(0.24f, 0.2f, 0.24f), c, new Vector3(0f, 0f, 90f), true);
                Prim(root, Cube, new Vector3(0f, -0.3f, 0.54f), Vector3.one * 0.1f, dark);
                break;
            case Acc.Scarf:
                Prim(root, Cube, new Vector3(0f, -0.2f, 0f), new Vector3(1.07f, 0.16f, 1.07f), c);
                Prim(root, Cube, new Vector3(0.26f, -0.42f, 0.54f), new Vector3(0.14f, 0.42f, 0.06f), c);
                Prim(root, Cube, new Vector3(0.26f, -0.6f, 0.54f), new Vector3(0.14f, 0.05f, 0.065f), light);
                break;
            case Acc.Cape:
                Prim(root, Cube, new Vector3(0f, -0.02f, -0.54f), new Vector3(0.92f, 0.96f, 0.06f), c);
                Prim(root, Cube, new Vector3(0f, 0.43f, -0.5f), new Vector3(1.04f, 0.1f, 0.14f), dark);
                break;
            case Acc.Wings:
                foreach (float s in new[] { -1f, 1f })
                {
                    Prim(root, Cube, new Vector3(s * 0.62f, 0.12f, -0.3f), new Vector3(0.46f, 0.58f, 0.04f), c, new Vector3(0f, s * 40f, s * 15f));
                    Prim(root, Cube, new Vector3(s * 0.74f, -0.06f, -0.36f), new Vector3(0.3f, 0.36f, 0.04f), light, new Vector3(0f, s * 40f, s * 15f));
                }
                break;
        }
    }

    /// <summary>Builds outfit number 'index' (0 = none) as a child "Outfit" of 'parent'; 'size' = the pet's edge length.</summary>
    private void BuildOutfit(Transform parent, int index, float size)
    {
        List<Outfit> list = OutfitList();
        if (index <= 0 || index >= list.Count) return;
        GameObject root = new GameObject("Outfit");
        root.transform.SetParent(parent, false);
        root.transform.localScale = Vector3.one * size;
        foreach (KeyValuePair<Acc, Color> piece in list[index].pieces) AddAccessory(root.transform, piece.Key, piece.Value);
    }

    /// <summary>Puts the pet's outfit on its body (replacing the old one).</summary>
    private void ApplyOutfit(Pet p)
    {
        if (p == null || p.body == null || clicker == null) return;
        Transform old = p.body.transform.Find("Outfit");
        if (old != null) { old.SetParent(null); Destroy(old.gameObject); }
        BuildOutfit(p.body.transform, p.outfit, clicker.PixelBaseSize);
        if (p.broken) // the Glass Pet is in pieces right now: the new outfit stays hidden until it re-forms
        {
            Transform fresh = p.body.transform.Find("Outfit");
            if (fresh != null) foreach (Renderer r in fresh.GetComponentsInChildren<Renderer>()) r.enabled = false;
        }
    }

    // ------------------------------------------------------------------
    // The name tag
    // ------------------------------------------------------------------

    private Pet tagPet;
    private float tagTimer;
    private GameObject tagCanvas;
    private RectTransform tagCanvasRect, tagRect;
    private TMP_Text tagLabel;
    private Sprite pencilSprite;

    private bool TagVisible => tagCanvas != null && tagCanvas.activeSelf;

    private void UpdateTag(bool allowed)
    {
        bool overTag = TagVisible && RectTransformUtility.RectangleContainsScreenPoint(tagRect, PointerPosition(), null);
        if (allowed && hovered != null && held == null) { tagPet = hovered; tagTimer = tagGraceSeconds; }
        else if (allowed && overTag) tagTimer = tagGraceSeconds;
        else tagTimer -= Time.unscaledDeltaTime;

        bool show = allowed && tagPet != null && tagPet.body != null && tagPet.body.activeSelf && tagTimer > 0f && held == null && !customizing;
        if (!show)
        {
            if (TagVisible) tagCanvas.SetActive(false);
            if (tagTimer <= 0f || held != null) tagPet = null;
            return;
        }

        EnsureTag();
        if (!tagCanvas.activeSelf) tagCanvas.SetActive(true);
        string name = NameOf(tagPet);
        if (tagLabel.text != name) tagLabel.text = name;
        float width = Mathf.Clamp(tagLabel.GetPreferredValues(name).x + 36f + 52f, 170f, 480f);
        tagRect.sizeDelta = new Vector2(width, 60f);

        Camera cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
        if (cam == null) return;
        Vector3 screen = cam.WorldToScreenPoint(tagPet.body.transform.position + Vector3.up * clicker.PixelBaseSize * 0.9f);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(tagCanvasRect, screen, null, out Vector2 local);
        Vector2 half = tagCanvasRect.rect.size * 0.5f;
        local.x = Mathf.Clamp(local.x, -half.x + width * 0.5f, half.x - width * 0.5f);
        local.y = Mathf.Clamp(local.y, -half.y + 10f, half.y - 70f);
        tagRect.anchoredPosition = local;
    }

    private void EnsureTag()
    {
        if (tagCanvas != null) return;
        PixelUIKit.EnsureEventSystem();
        tagCanvas = PixelUIKit.CreateCanvas("PixelPets Tag Canvas", tagSortingOrder, referenceResolution, true);
        tagCanvasRect = tagCanvas.GetComponent<RectTransform>();

        GameObject box = new GameObject("Name Tag", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(tagCanvas.transform, false);
        box.GetComponent<Image>().color = tagColor;
        tagRect = box.GetComponent<RectTransform>();
        tagRect.anchorMin = tagRect.anchorMax = new Vector2(0.5f, 0.5f);
        tagRect.pivot = new Vector2(0.5f, 0f);

        tagLabel = PixelUIKit.CreateText(font, box.transform, "Name", "", tagFontSize, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, Color.white);
        RectTransform lr = tagLabel.rectTransform;
        lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
        lr.offsetMin = new Vector2(18f, 4f); lr.offsetMax = new Vector2(-62f, -4f);
        tagLabel.enableAutoSizing = true;
        tagLabel.fontSizeMax = tagFontSize;
        tagLabel.fontSizeMin = 14f;

        // The pencil button.
        GameObject pencil = new GameObject("Pencil", typeof(RectTransform), typeof(Image), typeof(Button));
        pencil.transform.SetParent(box.transform, false);
        Image pi = pencil.GetComponent<Image>();
        if (pencilSprite == null) pencilSprite = BuildPencilSprite();
        pi.sprite = pencilSprite;
        pi.preserveAspect = true;
        pencil.GetComponent<Button>().targetGraphic = pi;
        RectTransform pr = pencil.GetComponent<RectTransform>();
        pr.anchorMin = pr.anchorMax = new Vector2(1f, 0.5f);
        pr.pivot = new Vector2(1f, 0.5f);
        pr.sizeDelta = new Vector2(44f, 44f);
        pr.anchoredPosition = new Vector2(-10f, 0f);
        pencil.GetComponent<Button>().onClick.AddListener(() => OpenCustomize(tagPet));
        tagCanvas.SetActive(false);
    }

    private static Sprite BuildPencilSprite()
    {
        const int n = 32;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        Vector2 a = new Vector2(5f, 5f), b = new Vector2(27f, 27f); // tip -> eraser
        Vector2 axis = (b - a).normalized;
        float len = (b - a).magnitude;
        Color32 clear = new Color32(0, 0, 0, 0), outline = new Color32(30, 25, 20, 255), graphite = new Color32(60, 60, 70, 255),
                wood = new Color32(230, 190, 130, 255), body = new Color32(250, 205, 40, 255), ferrule = new Color32(190, 195, 205, 255),
                eraser = new Color32(240, 120, 140, 255);
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f) - a;
                float u = Vector2.Dot(p, axis) / len;
                float d = Mathf.Abs(p.x * axis.y - p.y * axis.x);
                float half = u < 0.2f ? 4f * Mathf.Max(0f, u / 0.2f) : 4f;
                if (u < 0f || u > 1f || d > half) { tex.SetPixel(x, y, clear); continue; }
                Color32 c = d > half - 1.1f || u < 0.01f || u > 0.99f ? outline
                          : u < 0.07f ? graphite : u < 0.2f ? wood : u < 0.78f ? body : u < 0.86f ? ferrule : eraser;
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply(false, false);
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }

    // ------------------------------------------------------------------
    // The customization window (freezes the game)
    // ------------------------------------------------------------------

    private bool customizing;
    private Pet editPet;
    private GameObject customRoot;
    private TMP_InputField nameField;
    private TMP_Text outfitLabel;
    private RawImage previewImage;
    private Camera previewCam;
    private RenderTexture previewRT;
    private GameObject previewStudio;
    private Transform previewModel;
    private float previewYaw = 15f, previewPitch = 12f;
    private bool previewDragging;
    private const float StudioHeight = -3000f; // far below everything, so only the preview camera sees it

    private void OpenCustomize(Pet p)
    {
        if (p == null || p.body == null || PopupOpen || customizing) return;
        customizing = true;
        editPet = p;
        PopupOpen = true; // the same freeze as the "pet found" popup
        savedTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        if (TagVisible) tagCanvas.SetActive(false);
        tagPet = null;
        hovered = null;
        HoveringPet = false;
        if (blockingClicks) { blockingClicks = false; PixelClicker.ExternalClickBlock = PixelBank.HoseOn; }

        BuildPreviewStudio(p);
        BuildCustomizeWindow(p);
        PixelWindows.Register(this, 160, () => customizing, CloseCustomize);
        PixelAudio.Play("ui_click");
    }

    private void BuildPreviewStudio(Pet p)
    {
        previewStudio = new GameObject("Pet Preview Studio");
        previewStudio.transform.position = new Vector3(0f, StudioHeight, 0f);

        previewRT = new RenderTexture(560, 440, 24, RenderTextureFormat.ARGB32) { name = "Pet Preview" };
        GameObject camGo = new GameObject("Preview Camera");
        camGo.transform.SetParent(previewStudio.transform, false);
        previewCam = camGo.AddComponent<Camera>();
        previewCam.clearFlags = CameraClearFlags.SolidColor;
        previewCam.backgroundColor = previewBackground;
        previewCam.fieldOfView = 30f;
        previewCam.nearClipPlane = 0.1f;
        previewCam.farClipPlane = 30f;
        previewCam.targetTexture = previewRT;
        previewCam.allowHDR = false;
        previewCam.allowMSAA = true;
        camGo.transform.localPosition = new Vector3(0f, 0.3f, 4.6f); // in front of the pet (+z is its front), looking at it
        camGo.transform.LookAt(previewStudio.transform.position + new Vector3(0f, 0.2f, 0f));

        previewModel = new GameObject("Model").transform;
        previewModel.SetParent(previewStudio.transform, false);
        RebuildPreviewModel(p);
        previewModel.localRotation = Quaternion.Euler(previewPitch, previewYaw, 0f);
    }

    private void RebuildPreviewModel(Pet p)
    {
        for (int i = previewModel.childCount - 1; i >= 0; i--) { Transform c = previewModel.GetChild(i); c.SetParent(null); Destroy(c.gameObject); }
        GameObject root = new GameObject("Pet Visual");
        root.transform.SetParent(previewModel, false);
        int tier = TierOf(p.type);
        if (tier >= 0)
        {
            GameObject model = clicker.CreateDisplayPixel(tier, root.transform, 1f);
            if (model != null) { model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity; }
        }
        BuildOutfit(root.transform, p.outfit, 1f);
    }

    private void BuildCustomizeWindow(Pet p)
    {
        PixelUIKit.EnsureEventSystem();
        customRoot = PixelUIKit.CreateCanvas("PixelPets Customize Canvas", customizeSortingOrder, referenceResolution, true);

        GameObject dim = new GameObject("Dim", typeof(RectTransform), typeof(Image));
        dim.transform.SetParent(customRoot.transform, false);
        dim.GetComponent<Image>().color = dimColor;
        PixelUIKit.Stretch(dim.GetComponent<RectTransform>());

        GameObject box = new GameObject("Box", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(dim.transform, false);
        box.GetComponent<Image>().color = panelColor;
        RectTransform br = box.GetComponent<RectTransform>();
        br.anchorMin = br.anchorMax = br.pivot = new Vector2(0.5f, 0.5f);
        br.sizeDelta = new Vector2(860f, 940f);

        TMP_Text title = PixelUIKit.CreateText(font, box.transform, "Title", customizeTitle, 52f, TextAlignmentOptions.Center, FontStyles.Bold, titleColor);
        TopRow(title.rectTransform, -24f, 70f, 0f);

        TMP_Text nameLabel = PixelUIKit.CreateText(font, box.transform, "Name Label", nameLabelText, 26f, TextAlignmentOptions.MidlineLeft, FontStyles.Normal,
                                                   new Color(1f, 1f, 1f, 0.7f));
        TopRow(nameLabel.rectTransform, -104f, 34f, 0f);
        nameLabel.rectTransform.offsetMin = new Vector2(130f, nameLabel.rectTransform.offsetMin.y);

        nameField = PixelUIKit.CreateInputField(font, box.transform, "Name Field", new Vector2(600f, 64f), new Color(0.2f, 0.2f, 0.27f, 1f), Color.white, 34f, "pet name");
        nameField.characterLimit = maxNameLength;
        nameField.text = NameOf(p);
        RectTransform nr = nameField.GetComponent<RectTransform>();
        nr.anchorMin = nr.anchorMax = nr.pivot = new Vector2(0.5f, 1f);
        nr.sizeDelta = new Vector2(600f, 64f);
        nr.anchoredPosition = new Vector2(0f, -142f);
        nameField.onEndEdit.AddListener(_ => ApplyName());

        // The 3D view (drag to turn the pet) with the outfit arrows on either side.
        GameObject view = new GameObject("Preview", typeof(RectTransform), typeof(RawImage), typeof(EventTrigger));
        view.transform.SetParent(box.transform, false);
        previewImage = view.GetComponent<RawImage>();
        previewImage.texture = previewRT;
        RectTransform vr = view.GetComponent<RectTransform>();
        vr.anchorMin = vr.anchorMax = vr.pivot = new Vector2(0.5f, 1f);
        vr.sizeDelta = new Vector2(560f, 440f);
        vr.anchoredPosition = new Vector2(0f, -230f);
        EventTrigger trigger = view.GetComponent<EventTrigger>();
        AddTrigger(trigger, EventTriggerType.BeginDrag, _ => previewDragging = true);
        AddTrigger(trigger, EventTriggerType.EndDrag, _ => previewDragging = false);
        AddTrigger(trigger, EventTriggerType.Drag, data =>
        {
            Vector2 delta = ((PointerEventData)data).delta;
            previewYaw += delta.x * 0.6f;
            previewPitch = Mathf.Clamp(previewPitch - delta.y * 0.6f, -85f, 85f);
        });

        Button left = PixelUIKit.CreateButton(font, box.transform, "Previous Outfit", "<", new Vector2(110f, 170f), buttonColor, Color.white, 64f);
        Button right = PixelUIKit.CreateButton(font, box.transform, "Next Outfit", ">", new Vector2(110f, 170f), buttonColor, Color.white, 64f);
        foreach (KeyValuePair<Button, float> a in new[] { new KeyValuePair<Button, float>(left, -1f), new KeyValuePair<Button, float>(right, 1f) })
        {
            RectTransform ar = a.Key.GetComponent<RectTransform>();
            ar.anchorMin = ar.anchorMax = ar.pivot = new Vector2(0.5f, 1f);
            ar.sizeDelta = new Vector2(110f, 170f);
            ar.anchoredPosition = new Vector2(a.Value * (280f + 30f + 55f), -230f - 220f + 85f);
            int dir = (int)a.Value;
            a.Key.onClick.AddListener(() => ChangeOutfit(dir));
        }

        outfitLabel = PixelUIKit.CreateText(font, box.transform, "Outfit", "", 32f, TextAlignmentOptions.Center, FontStyles.Bold, Color.white);
        outfitLabel.enableAutoSizing = true;
        outfitLabel.fontSizeMax = 32f;
        outfitLabel.fontSizeMin = 18f;
        TopRow(outfitLabel.rectTransform, -690f, 60f, 40f);
        UpdateOutfitLabel();

        Button done = PixelUIKit.CreateButton(font, box.transform, "Done", doneText, new Vector2(320f, 80f), buttonColor, Color.white, 38f);
        RectTransform dr = done.GetComponent<RectTransform>();
        dr.anchorMin = dr.anchorMax = dr.pivot = new Vector2(0.5f, 0f);
        dr.anchoredPosition = new Vector2(0f, 36f);
        done.onClick.AddListener(CloseCustomize);
    }

    private static void TopRow(RectTransform r, float y, float height, float margin)
    {
        r.anchorMin = new Vector2(0f, 1f); r.anchorMax = new Vector2(1f, 1f); r.pivot = new Vector2(0.5f, 1f);
        r.offsetMin = new Vector2(margin, 0f); r.offsetMax = new Vector2(-margin, 0f);
        r.sizeDelta = new Vector2(r.sizeDelta.x, height);
        r.anchoredPosition = new Vector2(0f, y);
    }

    private static void AddTrigger(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> action)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(action);
        trigger.triggers.Add(entry);
    }

    private void ChangeOutfit(int direction)
    {
        if (editPet == null) return;
        int count = OutfitList().Count;
        editPet.outfit = ((editPet.outfit + direction) % count + count) % count;
        ApplyOutfit(editPet);          // the pet on the floor wears it right away
        RebuildPreviewModel(editPet);
        UpdateOutfitLabel();
        PixelAudio.Play("ui_click");
    }

    private void UpdateOutfitLabel()
    {
        if (outfitLabel == null || editPet == null) return;
        List<Outfit> list = OutfitList();
        int i = Mathf.Clamp(editPet.outfit, 0, list.Count - 1);
        outfitLabel.text = string.Format(outfitFormat, i + 1, list.Count, list[i].name);
    }

    private void ApplyName()
    {
        if (editPet == null || nameField == null) return;
        string n = nameField.text.Trim();
        editPet.customName = n == PetName(editPet.type) ? "" : n; // an empty name goes back to the default
        if (string.IsNullOrEmpty(n)) nameField.SetTextWithoutNotify(NameOf(editPet));
    }

    private void UpdateCustomize()
    {
        if (previewModel == null) return;
        if (!previewDragging) previewYaw += previewSpinSpeed * Time.unscaledDeltaTime;
        previewModel.localRotation = Quaternion.Euler(previewPitch, previewYaw, 0f);
    }

    private void CloseCustomize()
    {
        if (!customizing) return;
        ApplyName();
        customizing = false;
        editPet = null;
        PopupOpen = false;
        PixelWindows.Unregister(this);
        CleanupCustomize();
        Time.timeScale = savedTimeScale;
    }

    private void CleanupCustomize()
    {
        if (customRoot != null) Destroy(customRoot);
        customRoot = null;
        if (previewStudio != null) Destroy(previewStudio);
        previewStudio = null;
        previewCam = null;
        previewModel = null;
        if (previewRT != null) { previewRT.Release(); Destroy(previewRT); }
        previewRT = null;
        if (tagCanvas != null) Destroy(tagCanvas);
        tagCanvas = null;
        if (customizing) { customizing = false; PopupOpen = false; }
    }
}
