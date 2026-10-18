// ---- System notes ----
// PixelSortingHopperWindow.cs
//   The Sorting Hopper's DEVICE WINDOW (2026-10-18): left-click a placed hopper (PixelSortingHopper.HandleClick) to open it. A standard Fixed window
//   (PixelUIKit.CreateWindow, its own canvas, Escape closes it: PixelWindows 60) that does not pause the game and shows ONE hopper:
//   - FILTER: the chosen type's picture (PixelTierPicture, turning) and name, and a 'Select filter' button that swaps the lower part for a scrolling
//     grid of every UNLOCKED husk type (not fly-aways) plus 'None'; clicking one sets the filter (PixelSortingHopper.SetFilter) and swaps back.
//   - HOLDING n / room: a picture + count per held type, and a line saying where things go (filter -> Recycler, the rest -> the side tube).
//   - EMPTY OUT (everything out of the side tube) and CLOSE buttons.
//   The hopper calls Changed(this) when what it holds or its filter changes (the window redraws if it shows that hopper) and Forget(this) when it is
//   destroyed (the window closes). One window for every hopper: Open(other) just switches what it shows.
//   A future device with settings can copy this shape (or this could become a shared 'device window' builder once a second device needs one).
// ---------------------------------------------------
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PixelSortingHopperWindow : MonoBehaviour
{
    private static PixelSortingHopperWindow instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; }

    private PixelSortingHopper hopper;
    private PixelClicker clicker;
    private TMP_FontAsset font;
    private GameObject panel;
    private RawImage filterPicture;
    private TMP_Text filterName, filterNote, holdingTitle, statusLine;
    private Button selectButton;
    private GameObject holdingArea, pickerArea;
    private RectTransform holdingGrid, pickerContent;
    private ScrollRect pickerScroll;
    private GameObject pickerBar;
    private bool picking, dirty;
    private float spin;

    private const float Width = 720f, Height = 760f, Pad = 30f;

    /// <summary>Is the window open?</summary>
    public static bool IsOpen => instance != null && instance.panel != null && instance.panel.activeSelf;

    /// <summary>Opens the window for this hopper (or switches the open window to it).</summary>
    public static void Open(PixelSortingHopper h)
    {
        if (h == null) return;
        if (instance == null)
        {
            PixelClicker c = h.Clicker != null ? h.Clicker : PixelFind.Cached<PixelClicker>();
            if (c == null || c.UIFont == null) return;
            PixelUIKit.EnsureEventSystem();
            GameObject canvas = PixelUIKit.CreateCanvas("Sorting Hopper Window", 320, new Vector2(1920f, 1080f), true);
            instance = canvas.AddComponent<PixelSortingHopperWindow>();
            instance.clicker = c;
            instance.font = c.UIFont;
            instance.Build();
            PixelWindows.Register(instance, 60, () => IsOpen, Close);
        }
        instance.hopper = h;
        instance.picking = false;
        instance.panel.SetActive(true);
        instance.Refresh();
    }

    /// <summary>Closes the window.</summary>
    public static void Close()
    {
        if (instance == null || instance.panel == null) return;
        instance.panel.SetActive(false);
        instance.hopper = null;
    }

    /// <summary>The hopper's filter or contents changed: redraw if the window shows it.</summary>
    public static void Changed(PixelSortingHopper h) { if (instance != null && instance.hopper == h && h != null) instance.dirty = true; }

    /// <summary>The hopper is going away: close the window if it shows it.</summary>
    public static void Forget(PixelSortingHopper h) { if (instance != null && instance.hopper == h) Close(); }

    private void OnDestroy()
    {
        PixelWindows.Unregister(this);
        if (instance == this) instance = null;
    }

    // ---- Building ----

    private void Build()
    {
        PixelWindowParts parts = PixelUIKit.CreateWindow(font, transform, "Panel", "Sorting Hopper", new Vector2(Width, Height), PixelUIKit.WindowKind.Fixed, Close);
        panel = parts.Root;
        float top = parts.HeaderHeight + 16f;

        // The filter: picture, name + note, and the 'Select filter' button.
        GameObject pic = new GameObject("Filter Picture", typeof(RectTransform), typeof(RawImage));
        pic.transform.SetParent(panel.transform, false);
        filterPicture = pic.GetComponent<RawImage>();
        filterPicture.raycastTarget = false;
        Place(pic.GetComponent<RectTransform>(), new Vector2(Pad, -top), new Vector2(110f, 110f));
        filterName = Text("Filter Name", "", 34f, FontStyles.Bold, Color.white, new Vector2(Pad + 126f, -top - 6f), new Vector2(300f, 46f), TextAlignmentOptions.MidlineLeft);
        filterNote = Text("Filter Note", "", 22f, FontStyles.Normal, new Color(0.75f, 0.82f, 0.95f, 1f), new Vector2(Pad + 126f, -top - 54f), new Vector2(300f, 56f), TextAlignmentOptions.TopLeft);
        selectButton = PixelUIKit.CreateButton(font, panel.transform, "Select Filter", "Select filter", new Vector2(220f, 64f), ButtonRole.Primary, 26f);
        RectTransform sr = selectButton.GetComponent<RectTransform>();
        sr.anchorMin = sr.anchorMax = sr.pivot = new Vector2(1f, 1f);
        sr.anchoredPosition = new Vector2(-Pad, -top - 22f);
        selectButton.onClick.AddListener(() => { picking = !picking; Refresh(); });

        float areaTop = top + 140f, areaHeight = Height - areaTop - 110f;

        // What it holds (shown while not picking).
        holdingArea = Area("Holding", areaTop, areaHeight);
        holdingTitle = PixelUIKit.CreateText(font, holdingArea.transform, "Holding Title", "", 28f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, Color.white);
        Place(holdingTitle.rectTransform, Vector2.zero, new Vector2(Width - 2f * Pad, 40f));
        GameObject grid = new GameObject("Holding Grid", typeof(RectTransform));
        grid.transform.SetParent(holdingArea.transform, false);
        holdingGrid = grid.GetComponent<RectTransform>();
        Place(holdingGrid, new Vector2(0f, -50f), new Vector2(Width - 2f * Pad, areaHeight - 160f));
        statusLine = PixelUIKit.CreateText(font, holdingArea.transform, "Status", "", 22f, TextAlignmentOptions.TopLeft, FontStyles.Italic, new Color(0.75f, 0.82f, 0.95f, 1f));
        Place(statusLine.rectTransform, new Vector2(0f, -(areaHeight - 100f)), new Vector2(Width - 2f * Pad, 96f));

        // The type picker (shown while picking): a scrolling grid.
        pickerArea = Area("Picker", areaTop, areaHeight);
        pickerScroll = PixelUIKit.CreateScrollView(pickerArea.transform, "Picker Scroll", Color.gray, PixelUIKit.ScrollBarWidth, 30f, out pickerContent, out pickerBar);
        PixelUIKit.Stretch(pickerScroll.GetComponent<RectTransform>());

        // Bottom buttons.
        Button empty = PixelUIKit.CreateButton(font, panel.transform, "Empty Out", "Empty out", new Vector2(220f, 60f), ButtonRole.Danger, 26f);
        Bottom(empty, -130f);
        empty.onClick.AddListener(() => { if (hopper != null) hopper.EmptyOut(); });
        Button close = PixelUIKit.CreateButton(font, panel.transform, "Close", "Close", new Vector2(220f, 60f), ButtonRole.Neutral, 26f);
        Bottom(close, 130f);
        close.onClick.AddListener(Close);
        panel.SetActive(false);
    }

    private GameObject Area(string name, float top, float height)
    {
        GameObject a = new GameObject(name, typeof(RectTransform));
        a.transform.SetParent(panel.transform, false);
        Place(a.GetComponent<RectTransform>(), new Vector2(Pad, -top), new Vector2(Width - 2f * Pad, height));
        return a;
    }

    private static void Place(RectTransform r, Vector2 topLeft, Vector2 size)
    {
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = topLeft;
        r.sizeDelta = size;
    }

    private static void Bottom(Button b, float x)
    {
        RectTransform r = b.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0f);
        r.anchoredPosition = new Vector2(x, 26f);
    }

    private TMP_Text Text(string name, string text, float size, FontStyles style, Color color, Vector2 topLeft, Vector2 box, TextAlignmentOptions align)
    {
        TMP_Text t = PixelUIKit.CreateText(font, panel.transform, name, text, size, align, style, color);
        Place(t.rectTransform, topLeft, box);
        return t;
    }

    // ---- Showing ----

    private void Update()
    {
        if (!IsOpen) return;
        if (hopper == null) { Close(); return; }
        spin += Time.unscaledDeltaTime * 50f;
        if (hopper.FilterTier >= 0) PixelTierPicture.Render(hopper.FilterTier, spin);
        if (dirty) Refresh();
    }

    private string Name(int tier) => clicker.Tiers[tier].displayName.Replace(" Pixels", "");

    private void Refresh()
    {
        dirty = false;
        if (hopper == null || clicker == null) return;
        int f = hopper.FilterTier;
        filterPicture.texture = f >= 0 ? PixelTierPicture.Get(clicker, f, 160) : null;
        filterPicture.color = f >= 0 ? Color.white : new Color(1f, 1f, 1f, 0f);
        PixelUIKit.SetText(filterName, f >= 0 ? Name(f) : "No filter");
        PixelUIKit.SetText(filterNote, f >= 0 ? "Goes into the Recycler.\nEverything else drops out of the side tube."
                                              : "Pick a type to start sorting.\nUntil then it just holds husks.");
        selectButton.GetComponentInChildren<TMP_Text>().text = picking ? "Back" : "Select filter";
        holdingArea.SetActive(!picking);
        pickerArea.SetActive(picking);
        if (picking) BuildPicker(); else BuildHolding();
    }

    private void BuildHolding()
    {
        IReadOnlyList<PixelSortingHopper.Held> contents = hopper.Contents;
        PixelUIKit.SetText(holdingTitle, "Holding " + contents.Count + " / " + hopper.Room);
        // One tile per held type (in tier order), with its count.
        SortedDictionary<int, int> counts = new SortedDictionary<int, int>();
        foreach (PixelSortingHopper.Held h in contents) { counts.TryGetValue(h.tier, out int n); counts[h.tier] = n + 1; }
        Clear(holdingGrid);
        int i = 0;
        foreach (KeyValuePair<int, int> kv in counts)
        {
            if (!clicker.IsValidTierIndex(kv.Key)) continue;
            RectTransform tile = Tile(holdingGrid, i++, kv.Key, "x" + kv.Value, kv.Key == hopper.FilterTier);
            tile.GetComponent<Image>().raycastTarget = false;
        }
        string status = !hopper.OnRecycler ? "Not on a Recycler: filtered husks wait in the hopper. Place it behind a Recycler."
                      : hopper.FilterTier < 0 ? "Choose a filter: then that type goes into the Recycler and the rest drops out of the side tube."
                      : contents.Count == 0 ? "Empty. Drop husks into the bowl on top."
                      : "Sorting...";
        PixelUIKit.SetText(statusLine, status);
    }

    private void BuildPicker()
    {
        Clear(pickerContent);
        int i = 0;
        RectTransform none = Tile(pickerContent, i++, -1, "None", hopper.FilterTier < 0);
        PixelUIKit.MakeClickable(none.gameObject, () => Choose(-1));
        for (int t = 0; t < clicker.Tiers.Length; t++)
        {
            PixelClicker.PixelTier tier = clicker.Tiers[t];
            if (tier == null || !tier.unlocked || tier.flyAway) continue;
            RectTransform tile = Tile(pickerContent, i++, t, Name(t), t == hopper.FilterTier);
            int pick = t;
            PixelUIKit.MakeClickable(tile.gameObject, () => Choose(pick));
            PixelTierPicture.Render(t, 30f);
        }
        int rows = (i + Columns - 1) / Columns;
        PixelUIKit.UpdateScrollView(pickerScroll, pickerBar, rows * (TileH + Gap), pickerArea.GetComponent<RectTransform>().rect.height);
    }

    private void Choose(int tier)
    {
        if (hopper != null) hopper.SetFilter(tier);
        picking = false;
        Refresh();
    }

    private const float TileW = 118f, TileH = 132f, Gap = 10f;
    private const int Columns = 5;

    /// <summary>A tile at grid slot 'index': the block's picture (or nothing for None) and a label under it; 'selected' = highlighted.</summary>
    private RectTransform Tile(RectTransform parent, int index, int tier, string label, bool selected)
    {
        GameObject go = new GameObject("Tile", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image bg = go.GetComponent<Image>();
        bg.color = selected ? PixelUITheme.ColorOf(ButtonRole.Selected) : new Color(0f, 0f, 0f, 0.25f);
        PixelUIKit.StyleBox(bg, true);
        RectTransform r = go.GetComponent<RectTransform>();
        Place(r, new Vector2((index % Columns) * (TileW + Gap), -(index / Columns) * (TileH + Gap)), new Vector2(TileW, TileH));
        if (tier >= 0)
        {
            GameObject pic = new GameObject("Picture", typeof(RectTransform), typeof(RawImage));
            pic.transform.SetParent(go.transform, false);
            RawImage img = pic.GetComponent<RawImage>();
            img.texture = PixelTierPicture.Get(clicker, tier, 128);
            img.raycastTarget = false;
            Place(pic.GetComponent<RectTransform>(), new Vector2((TileW - 84f) * 0.5f, -6f), new Vector2(84f, 84f));
            PixelTierPicture.Render(tier, 30f);
        }
        TMP_Text t = PixelUIKit.CreateText(font, go.transform, "Label", label, 18f, TextAlignmentOptions.Center, FontStyles.Bold, Color.white);
        Place(t.rectTransform, new Vector2(4f, tier >= 0 ? -92f : -(TileH - 34f) * 0.5f), new Vector2(TileW - 8f, 34f));
        PixelUIKit.FitOneLine(t, 11f);
        return r;
    }

    private static void Clear(RectTransform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--) Destroy(parent.GetChild(i).gameObject);
    }
}
