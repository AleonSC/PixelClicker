// ---- System notes ----
// PixelConsumableGallery.cs
//   The 3D consumables window: opened by the square menu button between the Consumables tab's drop-down and its search box (PixelUI). A large
//   Fixed window (PixelUIKit.CreateWindow) that closes every other window when it opens (PixelWindows.CloseAllExcept) and shows the consumables
//   the player holds (PixelConsumables.ShownInInventory, the Inventory's own rule) as a grid of tiles with a REAL 3D model each (PixelModelPicture:
//   devices from PixelConsumables.CreateDisplayModel, potions / seeds / anything without a model as the block they belong to), the name and how
//   many. A drop-down filters: All / Devices / Potions / Seeds / Supplies (PixelConsumables.CategoryOf).
//   * HOVER a tile: it turns, and a box beside the pointer shows its description (PixelConsumables.ItemDescription).
//   * LEFT-CLICK: the window closes and the item is used (PixelConsumables.TryUseItem: a device goes straight into placing, a potion is drunk).
//   * RIGHT-CLICK: a solo window ABOVE it with a large turning model and the description in a box underneath (and a Use button). X / Escape closes it.
//   Tiles only redraw while hovered (each picture is drawn once otherwise); every studio is destroyed when the window closes (ReleaseAll).
//   Escape closes the solo window first, then this one (PixelWindows 36 / 35).
// ---------------------------------------------------
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PixelConsumableGallery : MonoBehaviour
{
    [Header("3D consumables window")]
    [Tooltip("Title of the window.")]
    [SerializeField] private string title = "Consumables";
    [Tooltip("Size of the window (canvas units, 1920 x 1080 reference).")]
    [SerializeField] private Vector2 windowSize = new Vector2(1320f, 820f);
    [Tooltip("Size of one tile (its picture is square, the name and count under it).")]
    [SerializeField] private Vector2 tileSize = new Vector2(220f, 270f);
    [Tooltip("The drop-down's filters, in order: All, Devices, Potions, Seeds, Supplies.")]
    [SerializeField] private string[] filterNames = { "All", "Utilities", "Potions", "Seeds", "Supplies" };
    [Tooltip("Shown when nothing matches the filter.")]
    [SerializeField] private string emptyText = "You don't hold any of these yet.";
    [Tooltip("The hint line under the drop-down.")]
    [SerializeField] private string hintText = "Click to use  -  right-click for a closer look";
    [Tooltip("Size of the solo (right-click) window.")]
    [SerializeField] private Vector2 soloSize = new Vector2(640f, 800f);
    [Tooltip("Label of the solo window's Use button.")]
    [SerializeField] private string useLabel = "Use";
    [Tooltip("Degrees a hovered / solo model turns per second.")]
    [SerializeField] private float spinSpeed = 60f;
    [Tooltip("Sorting order of the window's canvas (above the HUD and the Inventory).")]
    [SerializeField] private int sortingOrder = 320;

    private static PixelConsumableGallery instance;
    private PixelClicker clicker;
    private PixelConsumables consumables;
    private GameObject canvasGo;
    private RectTransform canvasRect;
    private PixelWindowParts window, solo;
    private TMP_Dropdown filter;
    private RectTransform gridContent;
    private ScrollRect scroll;
    private GameObject scrollBar;
    private TMP_Text emptyLabel, tipText, soloText;
    private RectTransform tipBox;
    private RawImage soloPicture;
    private int filterIndex, soloItem = -1, hoverItem = -1;
    private float spin;
    private readonly List<Tile> tiles = new List<Tile>();

    private const string KeyPrefix = "cgal.";

    private sealed class Tile { public int item; public GameObject root; public RawImage picture; public bool drawn; public string key; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; }

    /// <summary>Is the window open?</summary>
    public static bool IsOpen => instance != null && instance.window != null && PixelPop.IsOpen(instance.window.Root);

    /// <summary>The Consumables tab's menu button: opens (or closes) the window.</summary>
    public static void Toggle()
    {
        if (instance == null) return;
        if (IsOpen) instance.Close(); else instance.Open();
    }

    private void Awake()
    {
        instance = this;
        clicker = PixelFind.Cached<PixelClicker>();
        PixelWindows.Register(this, 35, () => IsOpen, Close);
        PixelWindows.Register(soloOwner, 36, () => solo != null && PixelPop.IsOpen(solo.Root), CloseSolo);
    }

    private readonly object soloOwner = new object();

    private void OnDestroy()
    {
        PixelWindows.Unregister(this);
        PixelWindows.Unregister(soloOwner);
        PixelModelPicture.ReleaseAll(KeyPrefix);
        if (canvasGo != null) Destroy(canvasGo);
        if (instance == this) instance = null;
    }

    // ------------------------------------------------------------------

    private void Open()
    {
        if (clicker == null) clicker = PixelFind.Cached<PixelClicker>();
        if (consumables == null) consumables = PixelFind.Cached<PixelConsumables>();
        if (clicker == null || consumables == null) return;
        PixelWindows.CloseAllExcept(this);
        if (window == null) Build();
        PixelPop.Show(window.Root);
        canvasGo.SetActive(true);
        RebuildTiles();
    }

    private void Close()
    {
        CloseSolo();
        HideTip();
        if (window != null) PixelPop.Hide(window.Root);
        ClearTiles();
        PixelModelPicture.ReleaseAll(KeyPrefix);
    }

    private void Build()
    {
        TMP_FontAsset font = clicker.UIFont;
        canvasGo = PixelUIKit.CreateCanvas("Consumables 3D", sortingOrder, new Vector2(1920f, 1080f), true);
        canvasRect = canvasGo.GetComponent<RectTransform>();
        window = PixelUIKit.CreateWindow(font, canvasGo.transform, "Consumables 3D Window", title, windowSize, PixelUIKit.WindowKind.Fixed, Close);
        float top = window.HeaderHeight + 10f, pad = 24f;

        filter = PixelUIKit.CreateDropdown(font, window.Root.transform, "Filter", new Vector2(300f, 54f), PixelUITheme.ColorOf(ButtonRole.Neutral),
                                           new Color(0.12f, 0.12f, 0.16f, 1f), Color.white, 26f);
        filter.options.Clear();
        foreach (string f in filterNames) filter.options.Add(new TMP_Dropdown.OptionData(f == "Devices" ? "Utilities" : f));   // (one name everywhere: the shop calls it Utilities)
        filter.SetValueWithoutNotify(0);
        filter.RefreshShownValue();
        filter.onValueChanged.AddListener(v => { filterIndex = v; RebuildTiles(); });
        Place(filter.GetComponent<RectTransform>(), pad, top, 300f, 54f);

        TMP_Text hint = PixelUIKit.CreateText(font, window.Root.transform, "Hint", hintText, 24f, TextAlignmentOptions.MidlineRight, FontStyles.Italic, PixelUITheme.DimTextColor);
        hint.raycastTarget = false;
        Place(hint.rectTransform, pad + 320f, top, windowSize.x - pad * 2f - 320f, 54f);

        float gridTop = top + 54f + 14f;
        scroll = PixelUIKit.CreateScrollView(window.Root.transform, "Grid", PixelUITheme.ColorOf(ButtonRole.Neutral), 12f, 40f, out gridContent, out scrollBar);
        Place(scroll.GetComponent<RectTransform>(), pad, gridTop, windowSize.x - pad * 2f, windowSize.y - gridTop - pad);

        emptyLabel = PixelUIKit.CreateText(font, window.Root.transform, "Empty", emptyText, 30f, TextAlignmentOptions.Center, FontStyles.Normal, PixelUITheme.DimTextColor);
        emptyLabel.raycastTarget = false;
        Place(emptyLabel.rectTransform, pad, gridTop + 40f, windowSize.x - pad * 2f, 60f);

        // The hover description box (on the canvas, above everything; it never catches the mouse).
        GameObject tip = new GameObject("Tip", typeof(RectTransform), typeof(Image));
        tip.transform.SetParent(canvasGo.transform, false);
        Image tipImage = tip.GetComponent<Image>();
        PixelUIKit.StyleWindow(tipImage, WindowShade.ToolWindow);
        tipImage.raycastTarget = false;
        tipBox = tip.GetComponent<RectTransform>();
        tipBox.anchorMin = tipBox.anchorMax = new Vector2(0.5f, 0.5f);
        tipText = PixelUIKit.CreateText(font, tip.transform, "Text", "", 24f, TextAlignmentOptions.TopLeft, FontStyles.Normal, PixelUITheme.TextColor);
        tipText.raycastTarget = false;
        tip.SetActive(false);

        window.Root.SetActive(false);
    }

    // ------------------------------------------------------------------
    // The grid
    // ------------------------------------------------------------------

    private bool Matches(int item)
    {
        if (!consumables.ShownInInventory(item)) return false;
        int cat = consumables.CategoryOf(item);   // 0 potions, 1 devices, 2 seeds, 3 supplies
        switch (filterIndex)
        {
            case 1: return cat == 1;
            case 2: return cat == 0;
            case 3: return cat == 2;
            case 4: return cat == 3;
            default: return true;
        }
    }

    private void ClearTiles()
    {
        foreach (Tile t in tiles) if (t.root != null) Destroy(t.root);
        tiles.Clear();
        hoverItem = -1;
    }

    private void RebuildTiles()
    {
        ClearTiles();
        TMP_FontAsset font = clicker.UIFont;
        float viewW = windowSize.x - 48f - 20f, gap = 16f;
        int cols = Mathf.Max(1, Mathf.FloorToInt((viewW + gap) / (tileSize.x + gap)));
        float startX = (viewW - (cols * tileSize.x + (cols - 1) * gap)) * 0.5f;
        int n = 0;
        for (int item = 0; item < consumables.ItemCount; item++)
        {
            if (!Matches(item)) continue;
            int captured = item;
            GameObject go = new GameObject("Tile " + consumables.ItemName(item), typeof(RectTransform), typeof(Image), typeof(PotionRowClick), typeof(PixelHoverTip));
            go.transform.SetParent(gridContent, false);
            Image bg = go.GetComponent<Image>();
            PixelUIKit.StyleBox(bg, true);
            bg.color = new Color(0.08f, 0.09f, 0.13f, 0.85f);
            Place(go.GetComponent<RectTransform>(), startX + (n % cols) * (tileSize.x + gap), (n / cols) * (tileSize.y + gap), tileSize.x, tileSize.y);

            Tile tile = new Tile { item = item, root = go, key = KeyPrefix + item };
            GameObject pic = new GameObject("Picture", typeof(RectTransform), typeof(RawImage));
            pic.transform.SetParent(go.transform, false);
            tile.picture = pic.GetComponent<RawImage>();
            tile.picture.raycastTarget = false;
            float side = tileSize.x - 24f;
            Place(tile.picture.rectTransform, 12f, 10f, side, side);
            tile.picture.texture = PixelModelPicture.Get(tile.key, BuildModel(item), 256);
            tile.picture.color = tile.picture.texture != null ? Color.white : Color.clear;

            TMP_Text name = PixelUIKit.CreateText(font, go.transform, "Name", consumables.ItemName(item), 22f, TextAlignmentOptions.Center, FontStyles.Bold, Color.white);
            name.raycastTarget = false;
            name.enableAutoSizing = true; name.fontSizeMin = 14f; name.fontSizeMax = 22f;
            Place(name.rectTransform, 8f, side + 12f, tileSize.x - 16f, 30f);
            int owned = consumables.ItemOwned(item);
            TMP_Text count = PixelUIKit.CreateText(font, go.transform, "Count", "x" + (owned >= 99999 ? PixelConsumables.OwnedText(owned) : owned.ToString()), 20f,
                                                   TextAlignmentOptions.Center, FontStyles.Normal, PixelUITheme.DimTextColor);
            count.raycastTarget = false;
            Place(count.rectTransform, 8f, side + 40f, tileSize.x - 16f, 24f);

            PotionRowClick click = go.GetComponent<PotionRowClick>();
            click.highlight = null;
            click.onLeftClick = () => UseItem(captured);
            click.onRightClick = () => OpenSolo(captured);
            PixelHoverTip hover = go.GetComponent<PixelHoverTip>();
            hover.onEnter = () => { hoverItem = captured; bg.color = new Color(0.16f, 0.2f, 0.3f, 0.95f); ShowTip(captured); };
            hover.onExit = () => { if (hoverItem == captured) hoverItem = -1; if (bg != null) bg.color = new Color(0.08f, 0.09f, 0.13f, 0.85f); HideTip(); };
            tiles.Add(tile);
            n++;
        }
        int rows = (n + cols - 1) / cols;
        float contentH = rows * (tileSize.y + gap);
        gridContent.sizeDelta = new Vector2(gridContent.sizeDelta.x, contentH);
        gridContent.anchoredPosition = Vector2.zero;
        PixelUIKit.UpdateScrollView(scroll, scrollBar, contentH, scroll.GetComponent<RectTransform>().rect.height);
        emptyLabel.gameObject.SetActive(n == 0);
    }

    /// <summary>The model of an item: a device's own display model, else the block it belongs to (potions, seeds).</summary>
    private System.Func<Transform, GameObject> BuildModel(int item) => parent =>
    {
        PixelConsumables.DeviceKind? kind = consumables.ItemKind(item);
        GameObject model = kind.HasValue ? consumables.CreateDisplayModel(kind.Value) : null;
        if (model == null)
        {
            int tier = clicker.IndexOf(consumables.ItemRequiredType(item));
            if (tier >= 0) model = clicker.CreateDisplayPixel(tier, parent, 1f);
        }
        return model;
    };

    private void UseItem(int item)
    {
        Close();
        if (consumables != null) consumables.TryUseItem(item);   // (a device goes straight into placing, with every window out of the way)
    }

    // ------------------------------------------------------------------
    // The solo (right-click) window
    // ------------------------------------------------------------------

    private void OpenSolo(int item)
    {
        HideTip();
        TMP_FontAsset font = clicker.UIFont;
        if (solo == null)
        {
            solo = PixelUIKit.CreateWindow(font, canvasGo.transform, "Consumable Solo Window", "", soloSize, PixelUIKit.WindowKind.Fixed, CloseSolo);
            float pad = 28f, side = soloSize.x - pad * 2f - 80f, top = solo.HeaderHeight + 6f;
            GameObject pic = new GameObject("Picture", typeof(RectTransform), typeof(RawImage));
            pic.transform.SetParent(solo.Root.transform, false);
            soloPicture = pic.GetComponent<RawImage>();
            soloPicture.raycastTarget = false;
            Place(soloPicture.rectTransform, (soloSize.x - side) * 0.5f, top, side, side);
            float boxTop = top + side + 10f, boxH = soloSize.y - boxTop - pad - 66f;
            GameObject box = new GameObject("Description Box", typeof(RectTransform), typeof(Image));
            box.transform.SetParent(solo.Root.transform, false);
            Image boxImage = box.GetComponent<Image>();
            PixelUIKit.StyleBox(boxImage, true);
            boxImage.color = new Color(0.06f, 0.07f, 0.1f, 0.9f);
            boxImage.raycastTarget = false;
            Place(box.GetComponent<RectTransform>(), pad, boxTop, soloSize.x - pad * 2f, boxH);
            soloText = PixelUIKit.CreateText(font, box.transform, "Description", "", 24f, TextAlignmentOptions.TopLeft, FontStyles.Normal, PixelUITheme.TextColor);
            soloText.raycastTarget = false;
            soloText.enableAutoSizing = true; soloText.fontSizeMin = 14f; soloText.fontSizeMax = 24f;
            PixelUIKit.Stretch(soloText.rectTransform);
            soloText.rectTransform.offsetMin = new Vector2(16f, 12f);
            soloText.rectTransform.offsetMax = new Vector2(-16f, -12f);
            Button use = PixelUIKit.CreateButton(font, solo.Root.transform, "Use", useLabel, new Vector2(220f, 54f), ButtonRole.Primary, 26f);
            Place(use.GetComponent<RectTransform>(), (soloSize.x - 220f) * 0.5f, soloSize.y - pad - 54f, 220f, 54f);
            use.onClick.AddListener(() => { int it = soloItem; CloseSolo(); if (it >= 0) UseItem(it); });
            solo.Root.SetActive(false);
        }
        soloItem = item;
        solo.Title.text = consumables.ItemName(item).ToUpperInvariant();
        soloPicture.texture = PixelModelPicture.Get(KeyPrefix + item, BuildModel(item), 512);   // (the tile's studio, drawn bigger)
        soloPicture.color = soloPicture.texture != null ? Color.white : Color.clear;
        soloText.text = consumables.ItemDescription(item);
        solo.Root.transform.SetAsLastSibling();
        if (tipBox != null) tipBox.SetAsLastSibling();
        PixelPop.Show(solo.Root);
    }

    private void CloseSolo()
    {
        if (solo != null && solo.Root.activeSelf) PixelPop.Hide(solo.Root);
        soloItem = -1;
    }

    // ------------------------------------------------------------------

    private void ShowTip(int item)
    {
        if (tipBox == null || (solo != null && PixelPop.IsOpen(solo.Root))) return;
        string text = "<b>" + consumables.ItemName(item) + "</b>\n" + consumables.ItemDescription(item);
        tipText.text = text;
        float w = 460f, h = tipText.GetPreferredValues(text, w - 32f, 0f).y + 28f;
        tipBox.sizeDelta = new Vector2(w, h);
        PixelUIKit.Stretch(tipText.rectTransform);
        tipText.rectTransform.offsetMin = new Vector2(16f, 12f);
        tipText.rectTransform.offsetMax = new Vector2(-16f, -12f);
        tipBox.SetAsLastSibling();
        tipBox.gameObject.SetActive(true);
    }

    private void HideTip() { if (tipBox != null) tipBox.gameObject.SetActive(false); }

    private void Update()
    {
        if (window == null || !window.Root.activeSelf) return;
        if (PixelCalm.InMenu) { Close(); return; }
        spin += Time.unscaledDeltaTime * spinSpeed;
        foreach (Tile t in tiles)
        {
            if (t.picture == null || t.picture.texture == null) continue;
            if (t.item == hoverItem) { PixelModelPicture.Render(t.key, spin); t.drawn = false; }   // (the hovered one turns)
            else if (!t.drawn) { PixelModelPicture.Render(t.key, 30f); t.drawn = true; }        // (the rest are drawn once, at a nice angle)
        }
        if (solo != null && PixelPop.IsOpen(solo.Root) && soloItem >= 0) PixelModelPicture.Render(KeyPrefix + soloItem, spin);
        if (tipBox != null && tipBox.gameObject.activeSelf)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, PixelInput.PointerPosition(), null, out Vector2 local);
            PixelUIKit.PlaceTooltip(tipBox, canvasRect, local);
        }
    }

    private static void Place(RectTransform r, float x, float y, float w, float h)
    {
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = new Vector2(x, -y);
        r.sizeDelta = new Vector2(w, h);
    }
}
