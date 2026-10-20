// ---- System notes (moved here from CLAUDE.md) ----
// PixelShopPictures.cs
//   Part of partial class PixelShop: rotating 3D pictures on shop rows/cards (AddRowPicture, AddPackPicture, Consumables tiles / buy bar; pixel
//   packs cycle through their reward tiers every pictureCycleSeconds, Value rows, Consumables tiles (still, clipped to the grid) and buy bar, minigame
//   packs use primitive models BuildMinigameModel for ghost/meteor/blackhole/bomb/pad/snake/sort/breakout - a new minigame needs a
//   case + its id in ModelledMinigames). One shared PixelModelPicture studio per distinct picture key ('shop:' + tier:<Type> / item:<name> /
//   game:<id>; pixel cubes framed by pictureCubeFraming, the rest fitted), drawn only while on screen (UpdatePictures); hidePictures, pictureResolution. Shop cost lines no longer print '(you have n)'
//   (NoHave strips it from old saved format strings).
// ---------------------------------------------------
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Part of <see cref="PixelShop"/>: small rotating 3D pictures on the shop's rows and cards (pixel packs, Value rows, the
/// Consumables cards and minigame packs). Each distinct picture is built once in a shared PixelModelPicture studio (shown by a
/// RawImage) and only the pictures that are on screen are redrawn.
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

    [Min(0.3f)]
    [Tooltip("How much of the picture a pixel / potion cube fills: the camera's half-height in cube widths. Smaller = bigger cube (0.85 fits a spinning cube with its corners just touching the frame).")]
    [SerializeField] private float pictureCubeFraming = 0.85f;

    [Tooltip("Size of the picture in the Consumables tab's buy bar.")]
    [SerializeField] private float cardPictureSize = 112f;

    private class PictureSlot
    {
        public RawImage image;
        public RectTransform rect;
        public GameObject frame;
        public Func<string> key;
        public bool inScroll;   // sits in the main scrolling list (only drawn while inside its viewport)
        public RectTransform clip;   // sits in another scroll view: only drawn while inside this viewport (instead of the main list's)
        public bool still;      // drawn once, not spun (the Consumables tiles: dozens of them, only the selected item spins in the buy bar)
    }

    private const string PictureKeyPrefix = "shop:";   // (the shop's studios in PixelModelPicture)
    private const float PictureTilt = 6f;               // the shop films its models almost level, a little from above
    private readonly HashSet<string> pictureNoModel = new HashSet<string>();   // keys with nothing to show (not retried every frame)
    private readonly HashSet<string> pictureDrawnOnce = new HashSet<string>(); // still slots draw only until their key is in here
    private readonly Dictionary<string, string> pictureStudioKeys = new Dictionary<string, string>();   // key -> 'shop:' + key (made once, not every frame)

    private string StudioKey(string key)
    {
        if (!pictureStudioKeys.TryGetValue(key, out string k)) { k = PictureKeyPrefix + key; pictureStudioKeys[key] = k; }
        return k;
    }
    private readonly List<PictureSlot> pictureSlots = new List<PictureSlot>();

    // ------------------------------------------------------------------
    // Adding pictures to the UI
    // ------------------------------------------------------------------

    /// <summary>A framed picture slot. 'key' is asked every frame (so it can cycle or follow a drop-down); null = show nothing.</summary>
    private PictureSlot AddPictureSlot(RectTransform parent, float size, Vector2 topLeft, bool middleLeft, Func<string> key, bool inScroll,
                                       RectTransform clip = null, bool still = false)
    {
        GameObject frame = new GameObject("Picture", typeof(RectTransform), typeof(Image));
        frame.transform.SetParent(parent, false);
        frame.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.08f, 1f);
        PixelUIKit.StyleBox(frame.GetComponent<Image>(), true);   // a sunken frame
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

        PictureSlot slot = new PictureSlot { image = img, rect = fr, frame = frame, key = key, inScroll = inScroll, clip = clip, still = still };
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

    // (the keys are asked for every frame for every picture on screen: built once per type instead of a new string each time)
    private static readonly Dictionary<PixelClicker.PixelType, string> tierKeys = new Dictionary<PixelClicker.PixelType, string>();

    /// <summary>The picture key of a pixel type ("tier:Red"), or null.</summary>
    private static string TierKey(PixelClicker.PixelType type)
    {
        if (!tierKeys.TryGetValue(type, out string key)) { key = "tier:" + type; tierKeys[type] = key; }
        return key;
    }

    private static readonly Vector3[] viewportCorners = new Vector3[4], slotCorners = new Vector3[4];

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

    /// <summary>A pack's picture key: its pixel types one after another, the Electrician's pixel, or its minigame's model (null = none).</summary>
    private Func<string> PackPictureKey(int packIndex)
    {
        ShopPack p = packs[packIndex];
        if (p.skillId == PixelSkills.ElectricianId) return () => TierKey(PixelClicker.PixelType.Electric);   // the pack's pixel
        if (p.rewardTiers != null && p.rewardTiers.Length > 0) return PackTierKey(packIndex);
        if (!string.IsNullOrEmpty(p.unlocksMinigame) && HasMinigameModel(p.unlocksMinigame))
        {
            string key = "game:" + p.unlocksMinigame;
            return () => key;
        }
        return null;
    }

    /// <summary>Called when a pack row is built: pixel packs and minigame packs get a picture.</summary>
    private void AddPackPicture(PackRow row, int packIndex)
    {
        Func<string> key = PackPictureKey(packIndex);
        if (key != null) AddRowPicture(row, key);
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
            Vector3[] vc = viewportCorners;
            viewport.GetWorldCorners(vc);
            vMin = vc[0].y; vMax = vc[1].y;
        }

        Vector3[] c = slotCorners;
        int newStudios = 0;
        foreach (PictureSlot s in pictureSlots)
        {
            if (s.frame == null || !s.rect.gameObject.activeInHierarchy) continue;
            if (s.clip != null)
            {
                s.clip.GetWorldCorners(viewportCorners);
                float cMin = viewportCorners[0].y, cMax = viewportCorners[1].y;
                s.rect.GetWorldCorners(c);
                if (c[1].y < cMin || c[0].y > cMax) continue;
            }
            else if (s.inScroll && viewport != null)
            {
                s.rect.GetWorldCorners(c);
                if (c[1].y < vMin || c[0].y > vMax) continue;
            }
            string key = s.key();
            if (key == null) { if (s.image.enabled) s.image.enabled = false; continue; }
            string studioKey = StudioKey(key);
            if (!PixelModelPicture.Has(studioKey) && !pictureNoModel.Contains(key) && ++newStudios > 4) continue;   // a grid full of new items builds its studios a few per frame, not all at once
            Texture tex = GetPicture(key);
            if (tex == null) { if (s.image.enabled) s.image.enabled = false; continue; }
            if (!s.image.enabled) s.image.enabled = true;
            if (s.image.texture != tex) s.image.texture = tex;
            if (s.still)
            {
                if (pictureDrawnOnce.Contains(key)) continue;   // (a spinning slot showing the same item keeps redrawing it, so the tile turns too while selected)
                pictureDrawnOnce.Add(key);
                PixelModelPicture.MarkDirty(studioKey);
                PixelModelPicture.Render(studioKey, 35f, PictureTilt);
                continue;
            }
            PixelModelPicture.Render(studioKey, Time.unscaledTime * pictureSpinSpeed, PictureTilt);   // (at most 30 redraws a second, once per frame when two rows show it)
            pictureDrawnOnce.Add(key);
        }
    }

    private void OnDestroyPictures()
    {
        PixelModelPicture.ReleaseAll(PictureKeyPrefix);
        pictureNoModel.Clear();
        pictureDrawnOnce.Clear();
    }

    // ------------------------------------------------------------------
    // Studios
    // ------------------------------------------------------------------

    /// <summary>The picture for a key (built on first use), or null when the key has nothing to show.</summary>
    private Texture GetPicture(string key)
    {
        if (pictureNoModel.Contains(key)) return null;
        Texture tex = PixelModelPicture.Existing(StudioKey(key));
        if (tex != null) return tex;
        // Pixels and potions are unit cubes: frame them by the cube itself (not by glow halos / rims, which made some tiny), as big as fits.
        tex = PixelModelPicture.Get(StudioKey(key), parent => BuildPictureModel(key), Mathf.Clamp(pictureResolution, 64, 512), PictureTilt,
                                            key.StartsWith("tier:") ? pictureCubeFraming : 0f);
        if (tex == null) pictureNoModel.Add(key);   // (remembers "no model" so it isn't retried every frame)
        return tex;
    }

    /// <summary>The model for a key with its range markings switched off (they don't belong in a picture or its framing).</summary>
    private GameObject BuildPictureModel(string key)
    {
        GameObject model = BuildModel(key);
        if (model == null) return null;
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
            if (PixelPlacedDevice.IsMarking(r)) r.enabled = false;
        return model;
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

    // ------------------------------------------------------------------
    // Minigame models (simple shapes built from primitives)
    // ------------------------------------------------------------------

    private static readonly string[] ModelledMinigames = { "ghost", "meteor", "blackhole", "bomb", "pad", "snake", "sort", "breakout", "dash", "blink", "evade", "traffic" };

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
            case "traffic":
            {
                // A traffic light: dark housing with red, yellow and green lamps (green lit).
                Prim(PrimitiveType.Cube, t, new Vector3(0f, 0f, 0f), new Vector3(0.42f, 1.15f, 0.2f), new Color(0.1f, 0.1f, 0.12f, 1f));
                Prim(PrimitiveType.Sphere, t, new Vector3(0f, 0.36f, -0.12f), Vector3.one * 0.26f, new Color(0.45f, 0.1f, 0.1f, 1f));
                Prim(PrimitiveType.Sphere, t, new Vector3(0f, 0f, -0.12f), Vector3.one * 0.26f, new Color(0.5f, 0.42f, 0.1f, 1f));
                Prim(PrimitiveType.Sphere, t, new Vector3(0f, -0.36f, -0.12f), Vector3.one * 0.26f, new Color(0.3f, 1f, 0.45f, 1f));
                break;
            }
            case "evade":
            {
                // A cube with a small mouse-pointer arrow chasing it.
                GameObject cube = Prim(PrimitiveType.Cube, t, new Vector3(0.3f, 0.1f, 0f), Vector3.one * 0.5f, new Color(0.4f, 0.9f, 0.5f, 1f));
                cube.transform.localRotation = Quaternion.Euler(20f, 35f, 0f);
                GameObject arrow = Prim(PrimitiveType.Cube, t, new Vector3(-0.45f, -0.25f, 0f), new Vector3(0.14f, 0.34f, 0.05f), Color.white);
                arrow.transform.localRotation = Quaternion.Euler(0f, 0f, -35f);
                Prim(PrimitiveType.Cube, t, new Vector3(-0.2f, -0.05f, 0f), new Vector3(0.3f, 0.04f, 0.04f), new Color(1f, 1f, 1f, 1f)).transform.localRotation = Quaternion.Euler(0f, 0f, 38f);
                break;
            }
            case "blink":
            {
                // A cube with a few shrinking afterimages around it.
                GameObject cube = Prim(PrimitiveType.Cube, t, new Vector3(0.25f, 0.05f, 0f), Vector3.one * 0.5f, new Color(0.6f, 0.4f, 1f, 1f));
                cube.transform.localRotation = Quaternion.Euler(20f, 35f, 0f);
                for (int i = 0; i < 3; i++)
                    Prim(PrimitiveType.Cube, t, new Vector3(-0.5f + i * 0.12f, 0.35f - i * 0.3f, 0f), Vector3.one * (0.26f - i * 0.07f), new Color(0.8f, 0.7f, 1f, 1f));
                break;
            }
            case "dash":
            {
                // A cube with motion streaks behind it, and a few faint afterimages.
                GameObject cube = Prim(PrimitiveType.Cube, t, new Vector3(0.3f, 0.05f, 0f), Vector3.one * 0.5f, new Color(1f, 0.85f, 0.25f, 1f));
                cube.transform.localRotation = Quaternion.Euler(20f, 35f, 0f);
                for (int i = 0; i < 3; i++)
                    Prim(PrimitiveType.Cube, t, new Vector3(-0.35f - i * 0.2f, 0.05f + (i - 1) * 0.16f, 0f), new Vector3(0.5f - i * 0.1f, 0.05f, 0.05f), new Color(1f, 1f, 1f, 1f));
                Prim(PrimitiveType.Cube, t, new Vector3(-0.15f, 0.05f, -0.05f), Vector3.one * 0.28f, new Color(0.9f, 0.7f, 0.2f, 1f));
                break;
            }
            default:
                Destroy(root);
                return null;
        }
        return root;
    }
}
