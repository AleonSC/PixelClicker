using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Part of <see cref="PixelShop"/>: small rotating 3D pictures on the shop's rows and cards (pixel packs, Value rows, the
/// Consumables cards and minigame packs). Each distinct picture is built once in a hidden studio far below the scene (a model, a
/// light and an orthographic camera that renders into a RenderTexture shown by a RawImage) and only the pictures that are on
/// screen are redrawn.
/// </summary>
public partial class PixelShop
{
    [Header("3D pictures")]
    [Tooltip("Turn off to remove the rotating 3D pictures from the shop's rows and cards.")]
    [SerializeField] private bool hidePictures = false;

    [Tooltip("Degrees per second the pictures turn.")]
    [SerializeField] private float pictureSpinSpeed = 45f;

    [Tooltip("Seconds each pixel type is shown for on a pack that unlocks several (RGB pack...).")]
    [SerializeField] private float pictureCycleSeconds = 2.5f;

    [Tooltip("Resolution (pixels) of every picture. Lower = cheaper.")]
    [SerializeField] private int pictureResolution = 160;

    [Tooltip("Size of the picture on a Consumables card.")]
    [SerializeField] private float cardPictureSize = 112f;

    private class ShopPicture
    {
        public GameObject root;
        public Transform pivot;
        public Camera cam;
        public RenderTexture rt;
    }

    private class PictureSlot
    {
        public RawImage image;
        public RectTransform rect;
        public GameObject frame;
        public Func<string> key;
        public bool inScroll;   // sits in the main scrolling list (only drawn while inside its viewport)
    }

    private const float PictureStudioHeight = -3900f;
    private readonly Dictionary<string, ShopPicture> pictures = new Dictionary<string, ShopPicture>();
    private readonly List<PictureSlot> pictureSlots = new List<PictureSlot>();
    private int pictureCounter;

    // ------------------------------------------------------------------
    // Adding pictures to the UI
    // ------------------------------------------------------------------

    /// <summary>A framed picture slot. 'key' is asked every frame (so it can cycle or follow a drop-down); null = show nothing.</summary>
    private PictureSlot AddPictureSlot(RectTransform parent, float size, Vector2 topLeft, bool middleLeft, Func<string> key, bool inScroll)
    {
        GameObject frame = new GameObject("Picture", typeof(RectTransform), typeof(Image));
        frame.transform.SetParent(parent, false);
        frame.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.08f, 1f);
        frame.GetComponent<Image>().raycastTarget = false;
        RectTransform fr = frame.GetComponent<RectTransform>();
        if (middleLeft)
        {
            fr.anchorMin = fr.anchorMax = fr.pivot = new Vector2(0f, 0.5f);
            fr.anchoredPosition = new Vector2(topLeft.x, 0f);
        }
        else
        {
            fr.anchorMin = fr.anchorMax = fr.pivot = new Vector2(0f, 1f);
            fr.anchoredPosition = new Vector2(topLeft.x, -topLeft.y);
        }
        fr.sizeDelta = new Vector2(size, size);

        GameObject imageGo = new GameObject("Model", typeof(RectTransform), typeof(RawImage));
        imageGo.transform.SetParent(frame.transform, false);
        RawImage img = imageGo.GetComponent<RawImage>();
        img.raycastTarget = false;
        PixelUIKit.Stretch(imageGo.GetComponent<RectTransform>());

        PictureSlot slot = new PictureSlot { image = img, rect = fr, frame = frame, key = key, inScroll = inScroll };
        pictureSlots.Add(slot);
        return slot;
    }

    /// <summary>Puts a picture at the left of a row and moves the row's texts right of it.</summary>
    private void AddRowPicture(PackRow row, Func<string> key)
    {
        if (hidePictures) return;
        float size = Mathf.Max(60f, rowHeight - 24f);
        AddPictureSlot(row.rect, size, new Vector2(14f, 0f), true, key, true);
        float shift = size + 14f;
        row.nameLabel.rectTransform.offsetMin += new Vector2(shift, 0f);
        row.costLabel.rectTransform.offsetMin += new Vector2(shift, 0f);
        Transform info = row.rect.Find("Info");
        if (info != null) info.GetComponent<RectTransform>().anchoredPosition += new Vector2(shift, 0f);
    }

    /// <summary>The picture key of a pixel type ("tier:Red"), or null.</summary>
    private static string TierKey(PixelClicker.PixelType type) => "tier:" + type;

    /// <summary>A picture that shows the pixel types of a pack one after another.</summary>
    private Func<string> PackTierKey(int packIndex)
    {
        return () =>
        {
            PixelClicker.PixelTier[] tiers = packs[packIndex].rewardTiers;
            if (tiers == null || tiers.Length == 0) return null;
            int n = tiers.Length;
            int i = n == 1 ? 0 : (int)(Time.unscaledTime / Mathf.Max(0.5f, pictureCycleSeconds)) % n;
            return TierKey(tiers[i].type);
        };
    }

    /// <summary>Called when a pack row is built: pixel packs and minigame packs get a picture.</summary>
    private void AddPackPicture(PackRow row, int packIndex)
    {
        ShopPack p = packs[packIndex];
        if (p.rewardTiers != null && p.rewardTiers.Length > 0) AddRowPicture(row, PackTierKey(packIndex));
        else if (!string.IsNullOrEmpty(p.unlocksMinigame) && HasMinigameModel(p.unlocksMinigame))
        {
            string key = "game:" + p.unlocksMinigame;
            AddRowPicture(row, () => key);
        }
    }

    /// <summary>Shrinks a card's title and drop-down and puts a picture at its top right.</summary>
    private void AddCardPicture(PurchaseCard card, RectTransform title, Func<string> key)
    {
        if (hidePictures) return;
        float size = cardPictureSize;
        float x = card.rect.sizeDelta.x - 14f - size;
        AddPictureSlot(card.rect, size, new Vector2(x, 8f), false, key, false);
        float narrow = size + 12f;
        title.sizeDelta = new Vector2(title.sizeDelta.x - narrow, title.sizeDelta.y);
        RectTransform dd = card.dropdown.GetComponent<RectTransform>();
        dd.sizeDelta = new Vector2(dd.sizeDelta.x - narrow, dd.sizeDelta.y);
    }

    // ------------------------------------------------------------------
    // Updating
    // ------------------------------------------------------------------

    private void UpdatePictures()
    {
        if (pictureSlots.Count == 0 || !panelObject.activeSelf) return;

        RectTransform viewport = scrollRect != null ? (scrollRect.viewport != null ? scrollRect.viewport : scrollRect.GetComponent<RectTransform>()) : null;
        float vMin = 0f, vMax = 0f;
        if (viewport != null)
        {
            Vector3[] vc = new Vector3[4];
            viewport.GetWorldCorners(vc);
            vMin = vc[0].y; vMax = vc[1].y;
        }

        Vector3[] c = new Vector3[4];
        foreach (PictureSlot s in pictureSlots)
        {
            if (s.frame == null || !s.rect.gameObject.activeInHierarchy) continue;
            if (s.inScroll && viewport != null)
            {
                s.rect.GetWorldCorners(c);
                if (c[1].y < vMin || c[0].y > vMax) continue;
            }
            string key = s.key();
            if (key == null) { if (s.image.enabled) s.image.enabled = false; continue; }
            ShopPicture pic = GetPicture(key);
            if (pic == null || pic.rt == null) { if (s.image.enabled) s.image.enabled = false; continue; }
            if (!s.image.enabled) s.image.enabled = true;
            if (s.image.texture != pic.rt) s.image.texture = pic.rt;
            pic.pivot.localRotation = Quaternion.Euler(0f, Time.unscaledTime * pictureSpinSpeed, 0f);
            pic.cam.Render();
        }
    }

    private void OnDestroyPictures()
    {
        foreach (ShopPicture p in pictures.Values)
        {
            if (p == null) continue;
            if (p.rt != null) p.rt.Release();
            if (p.root != null) Destroy(p.root);
        }
        pictures.Clear();
    }

    // ------------------------------------------------------------------
    // Studios
    // ------------------------------------------------------------------

    private ShopPicture GetPicture(string key)
    {
        if (pictures.TryGetValue(key, out ShopPicture existing)) return existing;
        GameObject model = BuildModel(key);
        ShopPicture pic = model != null ? BuildStudio(key, model) : null;
        pictures[key] = pic;   // a null entry remembers "no model" so it isn't retried every frame
        return pic;
    }

    private GameObject BuildModel(string key)
    {
        int colon = key.IndexOf(':');
        if (colon < 0) return null;
        string kind = key.Substring(0, colon), id = key.Substring(colon + 1);
        switch (kind)
        {
            case "tier":
            {
                if (!Enum.TryParse(id, out PixelClicker.PixelType type)) return null;
                int idx = clicker.IndexOf(type);
                if (idx < 0) return null;
                GameObject holder = new GameObject("Pixel Picture");
                GameObject px = clicker.CreateDisplayPixel(idx, holder.transform, 1f);
                if (px == null) { Destroy(holder); return null; }
                return holder;
            }
            case "item": return BuildItemPicture(id);
            case "game": return BuildMinigameModel(id);
        }
        return null;
    }

    private GameObject BuildItemPicture(string name)
    {
        if (consumables == null) return null;
        int item = -1;
        for (int i = 0; i < consumables.ItemCount; i++) if (consumables.ItemName(i) == name) { item = i; break; }
        if (item < 0) return null;

        GameObject root = new GameObject("Item Picture");
        if (!consumables.IsDevice(item))
        {
            // A potion: a glass cube with the pixel cube inside.
            int idx = clicker.IndexOf(consumables.PotionType(item));
            if (idx >= 0) clicker.CreateDisplayPixel(idx, root.transform, 0.55f);
            GameObject glass = GameObject.CreatePrimitive(PrimitiveType.Cube);
            glass.name = "Glass";
            Destroy(glass.GetComponent<Collider>());
            glass.transform.SetParent(root.transform, false);
            Material gm = clicker.CreateVisualMaterial(new Color(0.8f, 0.92f, 1f, 0.4f), true);
            if (gm != null) glass.GetComponent<Renderer>().sharedMaterial = gm;
            return root;
        }
        if (consumables.IsSeedItem(item))
        {
            PixelLooks.CreateSproutObject(root.transform, 1f);
            int idx = clicker.IndexOf(consumables.SeedTypeOf(item));
            if (idx >= 0)
            {
                GameObject px = clicker.CreateDisplayPixel(idx, root.transform, 0.4f);
                if (px != null) px.transform.localPosition = new Vector3(0f, PixelLooks.SproutStemTop + 0.22f, 0f);
            }
            return root;
        }
        Destroy(root);
        return consumables.CreateDisplayModel(consumables.DeviceKindOf(item));
    }

    private ShopPicture BuildStudio(string key, GameObject model)
    {
        GameObject root = new GameObject("Shop Studio " + key);
        root.transform.position = new Vector3(pictureCounter++ * 40f, PictureStudioHeight, 0f);
        GameObject pivot = new GameObject("Pivot");
        pivot.transform.SetParent(root.transform, false);
        model.transform.SetParent(pivot.transform, false);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        foreach (Collider col in model.GetComponentsInChildren<Collider>()) Destroy(col);

        Bounds bounds = new Bounds(model.transform.position, Vector3.zero);
        bool any = false;
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
        {
            if (r.name == "Range" || r.name == "Blow Area" || r.name == "Bend Cone") { r.enabled = false; continue; }
            if (r.GetComponent<TMP_Text>() != null) continue;
            if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);
        }
        model.transform.position += root.transform.position - bounds.center;
        float radius = Mathf.Max(0.3f, bounds.extents.magnitude);

        ShopPicture st = new ShopPicture { root = root, pivot = pivot.transform };
        int res = Mathf.Clamp(pictureResolution, 64, 512);
        st.rt = new RenderTexture(res, res, 24, RenderTextureFormat.ARGB32) { name = "Shop Picture " + key };
        GameObject camGo = new GameObject("Camera");
        camGo.transform.SetParent(root.transform, false);
        st.cam = camGo.AddComponent<Camera>();
        st.cam.enabled = false;
        st.cam.clearFlags = CameraClearFlags.SolidColor;
        st.cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        st.cam.orthographic = true;
        st.cam.orthographicSize = radius * 1.05f;
        st.cam.nearClipPlane = 0.1f;
        st.cam.farClipPlane = 60f;
        st.cam.allowHDR = false;
        st.cam.targetTexture = st.rt;
        camGo.transform.localPosition = new Vector3(0f, radius * 0.55f, -radius * 2.6f - 4f);
        camGo.transform.LookAt(root.transform.position);

        GameObject lightGo = new GameObject("Light");
        lightGo.transform.SetParent(root.transform, false);
        lightGo.transform.localPosition = new Vector3(2f * radius, 3f * radius, -5f * radius);
        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = 40f * radius;
        light.intensity = 7f;
        light.shadows = LightShadows.None;
        return st;
    }

    // ------------------------------------------------------------------
    // Minigame models (simple shapes built from primitives)
    // ------------------------------------------------------------------

    private static readonly string[] ModelledMinigames = { "ghost", "meteor", "blackhole", "bomb", "pad", "snake", "sort", "breakout" };

    private static bool HasMinigameModel(string id)
    {
        foreach (string m in ModelledMinigames) if (m == id) return true;
        return false;
    }

    private GameObject Prim(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Color color, bool glass = false)
    {
        GameObject g = GameObject.CreatePrimitive(type);
        Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        Material m = clicker.CreateVisualMaterial(color, glass || color.a < 0.99f);
        if (m != null) g.GetComponent<Renderer>().sharedMaterial = m;
        g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return g;
    }

    private GameObject BuildMinigameModel(string id)
    {
        GameObject root = new GameObject(id + " Picture");
        Transform t = root.transform;
        switch (id)
        {
            case "ghost":
            {
                Color body = new Color(0.9f, 0.95f, 1f, 0.75f);
                Prim(PrimitiveType.Sphere, t, new Vector3(0f, 0.15f, 0f), new Vector3(0.9f, 0.95f, 0.9f), body);
                Prim(PrimitiveType.Cylinder, t, new Vector3(0f, -0.2f, 0f), new Vector3(0.9f, 0.3f, 0.9f), body);
                Prim(PrimitiveType.Sphere, t, new Vector3(-0.17f, 0.25f, -0.38f), new Vector3(0.16f, 0.22f, 0.1f), new Color(0.08f, 0.08f, 0.12f, 1f));
                Prim(PrimitiveType.Sphere, t, new Vector3(0.17f, 0.25f, -0.38f), new Vector3(0.16f, 0.22f, 0.1f), new Color(0.08f, 0.08f, 0.12f, 1f));
                break;
            }
            case "meteor":
            {
                Color rock = new Color(0.32f, 0.2f, 0.14f, 1f), lava = new Color(1f, 0.5f, 0.1f, 1f);
                Prim(PrimitiveType.Sphere, t, Vector3.zero, new Vector3(1f, 0.85f, 0.95f), rock);
                Prim(PrimitiveType.Sphere, t, new Vector3(0.3f, 0.25f, -0.35f), new Vector3(0.28f, 0.2f, 0.2f), lava);
                Prim(PrimitiveType.Sphere, t, new Vector3(-0.25f, -0.1f, -0.4f), new Vector3(0.22f, 0.16f, 0.16f), lava);
                Prim(PrimitiveType.Sphere, t, new Vector3(0.05f, -0.35f, -0.3f), new Vector3(0.18f, 0.14f, 0.14f), lava);
                Prim(PrimitiveType.Cube, t, new Vector3(0.75f, 0.5f, 0f), new Vector3(1f, 0.28f, 0.28f), new Color(1f, 0.6f, 0.15f, 0.55f)).transform.localRotation = Quaternion.Euler(0f, 0f, 35f);
                break;
            }
            case "blackhole":
            {
                Prim(PrimitiveType.Sphere, t, Vector3.zero, Vector3.one * 0.7f, Color.black);
                Color ring = new Color(0.55f, 0.25f, 1f, 0.8f);
                GameObject a = Prim(PrimitiveType.Cylinder, t, Vector3.zero, new Vector3(1.5f, 0.02f, 1.5f), ring);
                a.transform.localRotation = Quaternion.Euler(70f, 0f, 0f);
                GameObject b = Prim(PrimitiveType.Cylinder, t, Vector3.zero, new Vector3(1.15f, 0.03f, 1.15f), new Color(0.9f, 0.6f, 1f, 0.9f));
                b.transform.localRotation = Quaternion.Euler(70f, 0f, 0f);
                break;
            }
            case "bomb":
            {
                Prim(PrimitiveType.Cube, t, Vector3.zero, new Vector3(1.4f, 0.8f, 0.5f), new Color(0.22f, 0.24f, 0.28f, 1f));
                Prim(PrimitiveType.Cube, t, new Vector3(0f, 0f, -0.27f), new Vector3(1.2f, 0.6f, 0.05f), new Color(0.12f, 0.13f, 0.16f, 1f));
                Prim(PrimitiveType.Cube, t, new Vector3(-0.35f, 0.42f, -0.2f), new Vector3(0.06f, 0.22f, 0.06f), new Color(0.9f, 0.15f, 0.15f, 1f));
                Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.42f, -0.2f), new Vector3(0.06f, 0.22f, 0.06f), new Color(0.2f, 0.85f, 0.25f, 1f));
                Prim(PrimitiveType.Cube, t, new Vector3(0.35f, 0.42f, -0.2f), new Vector3(0.06f, 0.22f, 0.06f), new Color(0.25f, 0.4f, 1f, 1f));
                Prim(PrimitiveType.Sphere, t, new Vector3(0.5f, -0.18f, -0.3f), Vector3.one * 0.14f, new Color(1f, 0.25f, 0.2f, 1f));
                break;
            }
            case "pad":
            {
                Prim(PrimitiveType.Cube, t, new Vector3(0f, -0.1f, 0f), new Vector3(1.4f, 0.1f, 1.4f), new Color(0.15f, 0.16f, 0.2f, 1f));
                Prim(PrimitiveType.Cube, t, new Vector3(0f, -0.04f, 0f), new Vector3(1.15f, 0.04f, 1.15f), new Color(0.2f, 0.85f, 0.7f, 1f));
                Prim(PrimitiveType.Cube, t, new Vector3(0f, -0.02f, 0f), new Vector3(0.95f, 0.05f, 0.95f), new Color(0.1f, 0.12f, 0.15f, 1f));
                GameObject cube = Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.3f, 0f), Vector3.one * 0.4f, new Color(1f, 0.82f, 0.2f, 1f));
                cube.transform.localRotation = Quaternion.Euler(20f, 30f, 0f);
                break;
            }
            case "snake":
            {
                Color green = new Color(0.25f, 0.8f, 0.3f, 1f);
                Vector3[] seg = { new Vector3(-0.6f, -0.3f, 0f), new Vector3(-0.2f, -0.3f, 0f), new Vector3(0.2f, -0.3f, 0f),
                                  new Vector3(0.2f, 0.1f, 0f), new Vector3(-0.2f, 0.1f, 0f), new Vector3(-0.2f, 0.5f, 0f) };
                for (int i = 0; i < seg.Length; i++)
                    Prim(PrimitiveType.Cube, t, seg[i], Vector3.one * (i == seg.Length - 1 ? 0.4f : 0.34f), i == seg.Length - 1 ? new Color(0.4f, 1f, 0.45f, 1f) : green);
                Prim(PrimitiveType.Sphere, t, new Vector3(-0.28f, 0.58f, -0.2f), Vector3.one * 0.07f, Color.black);
                Prim(PrimitiveType.Sphere, t, new Vector3(-0.12f, 0.58f, -0.2f), Vector3.one * 0.07f, Color.black);
                Prim(PrimitiveType.Cube, t, new Vector3(0.6f, 0.5f, 0f), Vector3.one * 0.22f, new Color(1f, 0.3f, 0.3f, 1f));
                break;
            }
            case "sort":
            {
                Color[] cols = { new Color(0.95f, 0.25f, 0.25f, 1f), new Color(0.3f, 0.85f, 0.3f, 1f), new Color(0.3f, 0.5f, 1f, 1f), new Color(1f, 0.85f, 0.2f, 1f) };
                for (int i = 0; i < 4; i++)
                {
                    float h = 0.35f + 0.22f * i;
                    Prim(PrimitiveType.Cube, t, new Vector3(-0.66f + i * 0.44f, -0.45f + h * 0.5f, 0f), new Vector3(0.38f, h, 0.38f), cols[i]);
                }
                break;
            }
            case "breakout":
            {
                Color[] rows = { new Color(0.95f, 0.3f, 0.3f, 1f), new Color(1f, 0.65f, 0.2f, 1f), new Color(0.3f, 0.85f, 0.4f, 1f) };
                for (int r = 0; r < 3; r++)
                    for (int c = 0; c < 4; c++)
                        Prim(PrimitiveType.Cube, t, new Vector3(-0.63f + c * 0.42f, 0.5f - r * 0.2f, 0f), new Vector3(0.38f, 0.16f, 0.2f), rows[r]);
                Prim(PrimitiveType.Sphere, t, new Vector3(0.1f, -0.25f, 0f), Vector3.one * 0.18f, Color.white);
                Prim(PrimitiveType.Cube, t, new Vector3(0f, -0.62f, 0f), new Vector3(0.7f, 0.12f, 0.2f), new Color(0.6f, 0.8f, 1f, 1f));
                break;
            }
            default:
                Destroy(root);
                return null;
        }
        return root;
    }
}
